using System.Diagnostics;
using System.Net.WebSockets;
using BepinexLogAnalysis;
using Discord;
using Discord.WebSocket;
using System.Text;
using System.Text.Json;

namespace WrenchMan;

internal class WrenchManBot : IDisposable
{
    protected readonly DiscordSocketClient? SocketClient;

    public static string BasePath { get; } = Directory.GetCurrentDirectory();
    public static string ConfigPath { get; } = Path.Combine(BasePath, "config");
    public static string GlobalConfigPath { get; } = Path.Combine(ConfigPath, "wrenchman.json");
    public static string GuildConfigsFolderPath { get; } = Path.Combine(ConfigPath, "guilds");
    public static string GuildConfigPath(string guildId) => Path.Combine(GuildConfigsFolderPath, $"{guildId}.json");

    private readonly HttpClient HttpClient = new();

    private WrenchConfig? _config;
    private readonly Dictionary<string, GuildSettings> _guildConfigs = [];

    private readonly LogAnalyzer _logAnalyzer;
    
    private readonly Dictionary<string, Func<SocketSlashCommand, Task>> _commandHandlers = [];
    
    private GuildSettings GetConfigForGuild(string guildId)
    {
        if (_guildConfigs.TryGetValue(guildId, out var config))
            return config;
        
        var path = GuildConfigPath(guildId);

        if (File.Exists(path))
            return _guildConfigs[guildId] = JsonSerializer.Deserialize<GuildSettings>(File.ReadAllText(path)) ?? throw new NullReferenceException("Guild config returned null!");
            
        config = _guildConfigs[guildId] = new GuildSettings();
        File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
        Program.Debug(nameof(WrenchManBot), $"Initialized new guild config for guild {guildId}");

        return config;
    }

    private void UpdateConfigForGuild(string guildId, bool silent = false)
    {
        var config = GetConfigForGuild(guildId);
        File.WriteAllText(GuildConfigPath(guildId), JsonSerializer.Serialize(config, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
        
        if (!silent)
            Program.Debug(nameof(WrenchManBot), $"Saved changes to guild config for {guildId}!");
    }

    private WrenchConfig GetGlobalConfig()
    {
        if (_config != null)
            return _config;

        if (File.Exists(GlobalConfigPath))
            return _config = JsonSerializer.Deserialize<WrenchConfig>(File.ReadAllText(GlobalConfigPath)) ?? throw new NullReferenceException("Config was null");
        
        File.WriteAllText(GlobalConfigPath, JsonSerializer.Serialize(_config = new(), new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
        Program.Debug(nameof(WrenchManBot), $"Initialized global config!");

        return _config;
    }

    private void UpdateGlobalConfig(bool silent = false)
    {
        File.WriteAllText(GlobalConfigPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
        
        if (!silent)
            Program.Debug(nameof(WrenchManBot), $"Saved changes to global config!");
    }

    public WrenchManBot()
    {
        Program.Info(nameof(WrenchManBot), "Started bot!");

        if (!Directory.Exists(ConfigPath))
            Directory.CreateDirectory(ConfigPath);

        if (!Directory.Exists(GuildConfigsFolderPath))
            Directory.CreateDirectory(GuildConfigsFolderPath);

        _ = GetGlobalConfig();
        UpdateGlobalConfig(silent: true);

        foreach (var file in Directory.EnumerateFiles(GuildConfigsFolderPath, "*.json"))
        {
            var guildId = Path.GetFileNameWithoutExtension(file);
            _guildConfigs[guildId] = GetConfigForGuild(guildId);
            UpdateConfigForGuild(guildId, silent: true);
        }
        
        Program.Debug(nameof(WrenchManBot), $"Loaded guild config settings for {_guildConfigs.Count} guilds.");
        
        LogAnalyzerConfig logAnalyzerConfig;

        if (!File.Exists(GetGlobalConfig().Settings.BepInExLogAnalysisRootConfigPath))
        {
            var matchersPath = Path.Combine(ConfigPath, "scoring_job_matchers");

            if (!Directory.Exists(matchersPath))
                Directory.CreateDirectory(matchersPath);

            File.WriteAllText(GetGlobalConfig().Settings.BepInExLogAnalysisRootConfigPath, JsonSerializer.Serialize(logAnalyzerConfig = new()
            {
                ScoringMatcherPaths =
                [
                    Path.Combine(matchersPath, "_global.json"),
                    Path.Combine(matchersPath, "atlyss.json"),
                ]
            }));
        }
        else
        {
            logAnalyzerConfig = JsonSerializer.Deserialize<LogAnalyzerConfig>(File.ReadAllText(GetGlobalConfig().Settings.BepInExLogAnalysisRootConfigPath)) ?? throw new NullReferenceException("Log analysis config was null");
        }

        _logAnalyzer = new LogAnalyzer(new LogAnalyzerOptions()
        {
            Config = logAnalyzerConfig,
            LogMethod = (level, logMessage) =>
            {
                Action<string, string> logger = level switch
                {
                    LogLevel.Fatal => Program.Fatal,
                    LogLevel.Error => Program.Error,
                    LogLevel.Warn => Program.Warn,
                    LogLevel.Info => Program.Info,
                    LogLevel.Debug => Program.Debug,
                    LogLevel.Trace => Program.Trace,
                    _ => (_, _) => { }
                };

                logger(nameof(LogAnalyzer), logMessage);
            }
        });

        string? token = Environment.GetEnvironmentVariable("WRENCHMAN_AUTH");

        if (token == null && File.Exists(GetGlobalConfig().TokenFilePath))
        {
            token = File.ReadAllText(GetGlobalConfig().TokenFilePath);
        }

        if (token == null)
        {
            Program.Warn(nameof(WrenchManBot), "Couldn't load Discord token for bot!");
            return;
        }

        SocketClient = new DiscordSocketClient(new DiscordSocketConfig
        {
            MessageCacheSize = 100,
            LogLevel = LogSeverity.Warning,
            AlwaysDownloadUsers = true,
            GatewayIntents = (GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers) & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents,
        });

        SocketClient.GuildAvailable += GuildAvailable;
        SocketClient.GuildUnavailable += GuildUnavailable;
        SocketClient.Log += SocketLog;

        SocketClient.MessageReceived += OnMessageReceived;
        SocketClient.SlashCommandExecuted += SlashCommandExecuted;
        SocketClient.Connected += OnConnected;
        SocketClient.Disconnected += OnDisconnected;

        SocketClient.Ready += DiscordReady;
        SocketClient.JoinedGuild += JoinedGuild;
        SocketClient.LeftGuild += LeftGuild;

        SocketClient.LoginAsync(TokenType.Bot, token);
        SocketClient.StartAsync();
    }

    private Task LeftGuild(SocketGuild guild)
    {
        Program.Info(nameof(WrenchManBot), $"Left guild {guild.Id}.");
        return Task.CompletedTask;
    }

    private Task JoinedGuild(SocketGuild guild)
    {
        Program.Info(nameof(WrenchManBot), $"Joined guild {guild.Id}.");
        return Task.CompletedTask;
    }

    private async Task SlashCommandExecuted(SocketSlashCommand cmd)
    {
        Program.Info(nameof(WrenchManBot), $"Processing command {cmd.Data.Name} from {cmd.User.Username} ({cmd.User.Id})...");
        await cmd.DeferAsync();

        try
        {
            if (!_commandHandlers.TryGetValue(cmd.Data.Name, out var handler))
            {
                await cmd.FollowupAsync("Uh, I don't know how to process that command...", ephemeral: true);
            }
            else
            {
                await handler(cmd);
            }
        }
        catch (Exception)
        {
            await cmd.FollowupAsync("I encountered an issue while processing this command! Please message the developer about this.", ephemeral: true);
            throw;
        }
    }

    private async Task DiscordReady()
    {
        List<ApplicationCommandProperties> cmds = [];

        _commandHandlers["analyze-log"] = AnalyzeLogCommand;
        cmds.Add(new SlashCommandBuilder()
            .WithName("analyze-log")
            .WithDescription("Extracts logs from arguments or a given message, and analyzes them.")
            .AddOption("file", ApplicationCommandOptionType.Attachment, "Attach a log file to analyze.", isRequired: true)
            .AddOption("sanitize", ApplicationCommandOptionType.Boolean, "Remove sensitive information from the log (such as Steam IDs)")
            .Build()
        );

        _commandHandlers["toggle-channel-logs"] = ToggleLogsForChannel;
        cmds.Add(new SlashCommandBuilder()
            .WithName("toggle-channel-logs")
            .WithDescription("Toggles on/off whenever the bot automatically analyzes logs sent in this channel.")
            .Build()
        );

        _commandHandlers["stats"] = GetBotStats;
        cmds.Add(new SlashCommandBuilder()
            .WithName("stats")
            .WithDescription("Gets bot stats.")
            .Build()
        );

        await SocketClient!.BulkOverwriteGlobalApplicationCommandsAsync(cmds.ToArray());
    }

    public void Dispose()
    {
        SocketClient?.Dispose();
        HttpClient.Dispose();
    }

    private readonly string[] CustomStatusFlavors =
    [
        "It was prod without a staging environment!!!",
        "Erectin' a Log Analyzer",
        "Stop sending me 6.7 MB log files",
        "Huh, wha?",
        "Yellow = crash, red = malware",
        "I forgor",
        "This wrench is actually just a prop",
        "ExceptionException: undefined"
    ];

    private async Task OnConnected()
    {
        Program.Debug(nameof(WrenchManBot), "Connected to Discord!");
        await SocketClient!.SetCustomStatusAsync(CustomStatusFlavors[Random.Shared.Next(0, CustomStatusFlavors.Length)]);
    }

    private Task OnDisconnected(Exception error)
    {
        Program.Debug(nameof(WrenchManBot), $"Got disconnected from Discord! {error.Message}");
        return Task.CompletedTask;
    }

    private async Task AnalyzeLogCommand(SocketSlashCommand cmd)
    {
        var attachmentOption = cmd.Data.Options.FirstOrDefault(x => x.Name == "file");

        if (attachmentOption == null)
        {
            await cmd.FollowupAsync($"You must provide a log to analyze!", ephemeral: true);
            return;
        }

        var attachment = (IAttachment)attachmentOption.Value;
        
        var sanitizeOption = cmd.Data.Options.FirstOrDefault(x => x.Name == "sanitize");

        bool sanitize = sanitizeOption != null ? (bool)sanitizeOption.Value : true;

        const int maxSizeInMib = 20;

        if (attachment.Size >= 1024 * 1024 * maxSizeInMib)
        {
            await cmd.FollowupAsync($"Sorry, I can only parse up to {maxSizeInMib} MiB of logs at once!", ephemeral: true);
            return;
        }

        IAttachment[] logsToAnalize = [attachment];

        var responseTime = Stopwatch.StartNew();
        
        await AnalyzeReceivedLogs(cmd.Channel, async (replyMessage, attachments) =>
        {
            if (attachments.Length == 0)
                await cmd.FollowupAsync(replyMessage);
            else
                await cmd.FollowupWithFilesAsync(attachments, replyMessage);
        }, logsToAnalize, true, sanitize, CancellationToken.None);

        Program.Debug(nameof(WrenchManBot), $"Response in total took {responseTime.ElapsedMilliseconds:F2}ms");
    }

    private async Task GetBotStats(SocketSlashCommand cmd)
    {
        if (!GetGlobalConfig().GlobalAdministrators.Contains(cmd.User.Id.ToString()))
        {
            await cmd.FollowupAsync("You cannot use that command!", ephemeral: true);
            return;
        }
        
        await cmd.FollowupAsync($"Currently in {SocketClient!.Guilds.Count} guilds", ephemeral: true);
    }

    private async Task ToggleLogsForChannel(SocketSlashCommand cmd)
    {
        if (cmd.Channel is not IGuildChannel channel)
        {
            await cmd.FollowupAsync("This command can only be used as part of servers!", ephemeral: true);
            return;
        }

        var guildUser = await channel.Guild.GetUserAsync(cmd.User.Id);

        if (!guildUser.GuildPermissions.ManageGuild && !GetGlobalConfig().GlobalAdministrators.Contains(cmd.User.Id.ToString()))
        {
            await cmd.FollowupAsync("You need the \"Manage Server\" to toggle channel logging!", ephemeral: true);
            return;
        }

        var config = GetConfigForGuild(channel.Guild.Id.ToString());
        var channelsToMonitor = config.LogAnalyzer.ChannelsToMonitor;

        if (channelsToMonitor.Contains(channel.Id.ToString()))
        {
            channelsToMonitor.Remove(channel.Id.ToString());
            Program.Info(nameof(WrenchManBot), $"Toggled off logging for channel {channel.Name} ({channel.Id}) in {channel.Guild.Name} ({channel.Guild.Id}).");
            await cmd.FollowupAsync("Toggled off logging for this channel!");
        }
        else
        {
            channelsToMonitor.Add(channel.Id.ToString());
            Program.Info(nameof(WrenchManBot), $"Toggled on logging for channel {channel.Name} ({channel.Id}) in {channel.Guild.Name} ({channel.Guild.Id}).");
            await cmd.FollowupAsync("Toggled on logging for this channel!");
        }
        
        UpdateConfigForGuild(channel.Guild.Id.ToString());
    }

    private async Task AnalyzeReceivedLogs(ISocketMessageChannel channel, Func<string, FileAttachment[], Task> reply, IAttachment[] attachments, bool resendLog, bool sanitizeLog, CancellationToken ct)
    {
        var messageLocationInfo = !GetGlobalConfig().LogUserAndLocationDetails
            ? ""
            : channel switch
            {
                IGuildChannel fromGuild => $"in server {fromGuild.Guild.Name} ({fromGuild.Guild.Id})",
                IDMChannel fromChannel => $"in direct messages with {fromChannel.Recipient.Username} ({fromChannel.Recipient.Id})",
                _ => $"in unknown channel ({channel.GetType()})"
            };

        Program.Info(nameof(WrenchManBot), $"Processing {attachments.Length} logs ({attachments.Select(x => x.Size).Sum() / 1024} KiB) {messageLocationInfo}...");

        var contents = await Task.WhenAll(attachments.Select(x => FetchAsync(x.Url, ct)));
        var logAttachments = resendLog ? new FileAttachment?[attachments.Length] : [];
        var reportAttachments = new FileAttachment?[attachments.Length];

        await Parallel.ForAsync(0, attachments.Length, async (index, cancellationToken) =>
        {
            var data = contents[index];

            if (data == null)
                return;

            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(data));

            if (sanitizeLog)
                stream = Sanitizer.Sanitize(stream);
            
            var result = await ProcessAttachment(stream, cancellationToken);

            if (result != null)
            {
                if (resendLog)
                {
                    stream.Position = 0;
                    logAttachments[index] = new FileAttachment(stream, $"Log-{index}.txt");
                }
                
                reportAttachments[index] = new FileAttachment(result, $"Report-{index}.txt");
            }
        });

        var actualLogs = logAttachments.Where(x => x != null).Select(x => x!.Value).ToArray();
        var actualReports = reportAttachments.Where(x => x != null).Select(x => x!.Value).ToArray();
        var replyAttachments = actualLogs.Concat(actualReports).ToArray();

        if (actualReports.Length == 0)
        {
            await reply("I couldn't retrieve or process any of the log files you sent!", []);
        }
        else if (actualReports.Length != attachments.Length)
        {
            await reply($"I couldn't retrieve or process {attachments.Length - actualReports.Length} log files from the ones you sent! Here's what I managed to process:", replyAttachments);
        }
        else
        {
            await reply($"Here's a summary of your log files!", replyAttachments);
        }
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (!ShouldProcessReceivedChannelMessage(message))
            return;

        var logsToAnalize = message.Attachments
            .Where(x => x.Filename.EndsWith(".log") || x.Filename.EndsWith(".txt"))
            .ToArray<IAttachment>();
        
        if (logsToAnalize.Length == 0)
            return;

        if (logsToAnalize.Length > 5)
        {
            await message.Channel.SendMessageAsync("Sorry, I can only process at most 5 logs per message!");
            return;
        }

        var totalSize = logsToAnalize.Sum(x => x.Size);
        
        const int maxSizeInMib = 20;
        
        if (totalSize >= 1024 * 1024 * maxSizeInMib)
        {
            await message.Channel.SendMessageAsync($"Sorry, I can only parse logs that have a total size of at most {maxSizeInMib} MiB!");
            return;
        }

        var responseTime = Stopwatch.StartNew();

        await AnalyzeReceivedLogs(message.Channel, async (replyMessage, attachments) =>
        {
            if (attachments.Length == 0)
                await message.Channel.SendMessageAsync(replyMessage);
            else
                await message.Channel.SendFilesAsync(attachments, replyMessage);
        }, logsToAnalize, false, false, CancellationToken.None);

        Program.Debug(nameof(WrenchManBot), $"Response in total took {responseTime.ElapsedMilliseconds:F2}ms");
    }

    private bool ShouldProcessReceivedChannelMessage(SocketMessage message)
    {
        if (message.Author.IsBot || message.Author.IsWebhook || message.Source == MessageSource.System)
            return false;

        var channelType = message.Channel.GetChannelType();

        if (channelType == ChannelType.DM && !GetGlobalConfig().Settings.LogAnalyzer.LookInDirectMessages)
            return false;

        if (message.Channel is SocketGuildChannel guildChannel)
        {
            var config = GetConfigForGuild(guildChannel.Guild.Id.ToString()).LogAnalyzer;

            if (!config.ChannelsToMonitor.Contains(guildChannel.Id.ToString()))
                return false;
        }

        return true;
    }
    
    private Task SocketLog(LogMessage message)
    {
        if (message.Exception is GatewayReconnectException or WebSocketException)
            return Task.CompletedTask; // Supress these since they're handled by the disconnect callback

        Action<string, string> logger = message.Severity switch
        {
            LogSeverity.Critical => Program.Fatal,
            LogSeverity.Error => Program.Error,
            LogSeverity.Warning => Program.Warn,
            LogSeverity.Info => Program.Info,
            LogSeverity.Debug => Program.Debug,
            LogSeverity.Verbose => Program.Trace,
            _ => (_, _) => { }
        };

        logger("Discord", message.ToString());
        return Task.CompletedTask;
    }

    private Task GuildUnavailable(SocketGuild guild)
    {
        return Task.CompletedTask;
    }

    private async Task GuildAvailable(SocketGuild guild)
    {
        var config = GetConfigForGuild(guild.Id.ToString());

        if (config.Blacklisted)
        {
            Program.Warn(nameof(WrenchManBot), $"Guild {guild.Id} was blacklisted, will attempt leaving.");
            await guild.LeaveAsync();
            return;
        }

        if (GetGlobalConfig().GuildWhitelist.Length > 0 && !GetGlobalConfig().GuildWhitelist.Contains(guild.Id.ToString()))
        {
            Program.Warn(nameof(WrenchManBot), $"Guild {guild.Id} is not in the whitelist, will attempt leaving.");
            await guild.LeaveAsync();
            return;
        }

        var self = guild.GetUser(SocketClient!.CurrentUser.Id);

        // TODO: This is a temporary setup
        // In the case this bot's functionality gets expanded, configuring and checking the permissions granularly
        // would make more sense
        if (self == null)
        {
            Program.Warn(nameof(WrenchManBot), $"Unable to check permission configuration for {guild.Id}!");
        }
        else
        {
            var perms = self.GuildPermissions;

            // Should correspond to permissions integer 274878008320
            bool permissionsMissing =
                !perms.AttachFiles ||
                !perms.SendMessages ||
                !perms.SendMessagesInThreads ||
                !perms.ReadMessageHistory ||
                !perms.ViewChannel;

            if (permissionsMissing)
                Program.Warn(nameof(WrenchManBot), $"Guild {guild.Id} might have misconfigured permissions for the bot!");
        }

        // No guild specific commands yet
        await guild.BulkOverwriteApplicationCommandAsync([]);
    }

    protected async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await HttpClient.GetAsync(url, ct);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            Program.Error(nameof(WrenchManBot), $"Failed to fetch resource from {url}: {e.Message}");
            return null;
        }
    }

    private async Task<Stream?> ProcessAttachment(Stream attachment, CancellationToken ct)
    {
        var minimumProcessingTime = Task.Delay(750);

        MemoryStream output = new();

        bool success = await _logAnalyzer.ProcessLogAsync(attachment, output, ct);
        await minimumProcessingTime;

        if (!success)
        {
            return null;
        }
        else
        {
            output.Position = 0;
            return output;
        }
    }
}