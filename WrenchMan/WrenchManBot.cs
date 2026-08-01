using System.Diagnostics;
using System.Net.WebSockets;
using BepinexLogAnalysis;
using Discord;
using Discord.WebSocket;
using System.Text.Json;

namespace WrenchMan;

public partial class WrenchManBot : IDisposable
{
    protected readonly DiscordSocketClient SocketClient;

    public static string BasePath { get; } = Directory.GetCurrentDirectory();
    public static string ConfigPath { get; } = Path.Combine(BasePath, "config");
    public static string GlobalConfigPath { get; } = Path.Combine(ConfigPath, "wrenchman.json");
    public static string GuildConfigsFolderPath { get; } = Path.Combine(ConfigPath, "guilds");
    public static string GuildConfigPath(string guildId) => Path.Combine(GuildConfigsFolderPath, $"{guildId}.json");

    private readonly HttpClient HttpClient = new();

    private WrenchConfig? _config;
    private readonly Dictionary<string, GuildSettings> _guildConfigs = [];
    private string? _token;

    private readonly LogAnalyzer _logAnalyzer;
    
    private readonly Dictionary<string, Func<SocketSlashCommand, Task>> _slashCommandHandlers = [];
    private readonly Dictionary<string, Func<SocketMessageCommand, Task>> _messageCommandHandlers = [];
    
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

        var globalConfig = GetGlobalConfig();
        UpdateGlobalConfig(silent: true);

        foreach (var file in Directory.EnumerateFiles(GuildConfigsFolderPath, "*.json"))
        {
            var guildId = Path.GetFileNameWithoutExtension(file);
            _guildConfigs[guildId] = GetConfigForGuild(guildId);
            UpdateConfigForGuild(guildId, silent: true);
        }
        
        Program.Debug(nameof(WrenchManBot), $"Loaded guild config settings for {_guildConfigs.Count} guilds.");
        
        LogAnalyzerConfig logAnalyzerConfig;

        if (!File.Exists(globalConfig.Settings.BepInExLogAnalysisRootConfigPath))
        {
            var matchersPath = Path.Combine(ConfigPath, "scoring_job_matchers");

            if (!Directory.Exists(matchersPath))
                Directory.CreateDirectory(matchersPath);

            File.WriteAllText(globalConfig.Settings.BepInExLogAnalysisRootConfigPath, JsonSerializer.Serialize(logAnalyzerConfig = new()));
        }
        else
        {
            logAnalyzerConfig = JsonSerializer.Deserialize<LogAnalyzerConfig>(File.ReadAllText(globalConfig.Settings.BepInExLogAnalysisRootConfigPath)) ?? throw new NullReferenceException("Log analysis config was null");
        }

        Program.Info(nameof(WrenchManBot), $"Will try to use {logAnalyzerConfig.BuiltinRulesToUse.Count} bundles rules: [{string.Join(", ", logAnalyzerConfig.BuiltinRulesToUse)}].");

        var options = new LogAnalyzerOptions
        {
            RuleLists =
            [
                .. BundledRules.All.Where(x => logAnalyzerConfig.BuiltinRulesToUse.Contains(x.Key))
                    .Select(x => x.Value),
                .. LoadExtraRules(logAnalyzerConfig.AdditionalRulePaths)
            ]
        };
        
        Program.Info(nameof(WrenchManBot), $"Using {options.RuleLists.Count} rules: [{string.Join(", ", options.RuleLists.Select(x => x.Name))}].");
        
        _logAnalyzer = new LogAnalyzer(options);

        string? token = Environment.GetEnvironmentVariable("WRENCHMAN_AUTH");

        if (token == null && File.Exists(globalConfig.TokenFilePath))
        {
            token = File.ReadAllText(globalConfig.TokenFilePath);
        }

        if (token == null)
            throw new InvalidOperationException("Couldn't load Discord token for bot!");

        _token = token;

        var gatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents;

        if (globalConfig.UsePrivilegedIntents)
            gatewayIntents = gatewayIntents | GatewayIntents.MessageContent | GatewayIntents.GuildMembers;

        SocketClient = new DiscordSocketClient(new DiscordSocketConfig
        {
            MessageCacheSize = 100,
            LogLevel = LogSeverity.Warning,
            AlwaysDownloadUsers = false,
            GatewayIntents = gatewayIntents,
        });

        SocketClient.GuildAvailable += GuildAvailable;
        SocketClient.GuildUnavailable += GuildUnavailable;
        SocketClient.Log += SocketLog;

        SocketClient.MessageReceived += OnMessageReceived;
        SocketClient.SlashCommandExecuted += SlashCommandExecuted;
        SocketClient.MessageCommandExecuted += MessageCommandExecuted;
        SocketClient.Connected += OnConnected;
        SocketClient.Disconnected += OnDisconnected;

        SocketClient.Ready += DiscordReady;
        SocketClient.JoinedGuild += JoinedGuild;
        SocketClient.LeftGuild += LeftGuild;
    }

    private static IEnumerable<LogRuleList> LoadExtraRules(IEnumerable<string> paths)
    {
        foreach (var filePath in paths)
        {
            if (!File.Exists(filePath))
            {
                Program.Warn(nameof(WrenchManBot), $"Skipping log rule path \"{filePath}\": rule file does not exist.");
                continue;
            }

            LogRuleList? ruleList = null;
            try
            {
                using var stream = File.OpenRead(filePath);
                ruleList = JsonSerializer.Deserialize<LogRuleList>(stream) ?? throw new InvalidOperationException("Deserializer returned null");
                
                Program.Info(nameof(WrenchManBot), $"Loaded extra rule list \"{filePath}\".");
            }
            catch (Exception e)
            {
                Program.Warn(nameof(WrenchManBot), $"Failed to load extra rule list \"{filePath}\": matcher file threw an exception on load: {e}");
            }

            if (ruleList != null)
                yield return ruleList;
        }
    }

    private TaskCompletionSource? _runState;

    public async Task Start()
    {
        if (_runState != null)
            return;

        _runState = new TaskCompletionSource();
        await SocketClient.LoginAsync(TokenType.Bot, _token);
        await SocketClient.StartAsync();
    }

    public async Task WaitForClose()
    {
        if (_runState != null)
            await _runState.Task;
    }

    public async Task Stop()
    {
        if (_runState == null)
            return;

        await SocketClient.StopAsync();
        _runState.TrySetResult();
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
        Program.Info(nameof(WrenchManBot), $"Processing slash command {cmd.Data.Name} from {cmd.User.Username} ({cmd.User.Id})...");
        await cmd.DeferAsync();

        try
        {
            if (!_slashCommandHandlers.TryGetValue(cmd.Data.Name, out var handler))
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
    
    private async Task MessageCommandExecuted(SocketMessageCommand cmd)
    {
        Program.Info(nameof(WrenchManBot), $"Processing message command {cmd.Data.Name} from {cmd.User.Username} ({cmd.User.Id})...");
        await cmd.DeferAsync();

        try
        {
            if (!_messageCommandHandlers.TryGetValue(cmd.Data.Name, out var handler))
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
        await SetupCommands();
    }

    public void Dispose()
    {
        SocketClient.Dispose();
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
        "ExceptionException: undefined",
        "Swisches my big fluffy $$$$-ing tail",
        "DID YOU SEE THAT? HUH???",
        "True!",
        "False...",
        "You owe me $5",
        "& Homebrewery",
        "Who's Joe...?",
        "Also available on Blunderstore",
        "Powered by furry queers",
        "Go play Casualties: Unknown!",
        "Go play ATLYSS!",
        "Go play Secrets of Grindea!",
        "*vine boom*",
        "SIX SEVEN SIX SEVEN SI-"
    ];

    private async Task OnConnected()
    {
        Program.Debug(nameof(WrenchManBot), "Connected to Discord!");
        await SocketClient.SetCustomStatusAsync(CustomStatusFlavors[Random.Shared.Next(0, CustomStatusFlavors.Length)]);
    }

    private Task OnDisconnected(Exception error)
    {
        Program.Debug(nameof(WrenchManBot), $"Got disconnected from Discord! {error.Message}");
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage)
            return; // We can't do much with system messages
        
        if (!ShouldProcessReceivedChannelMessage(userMessage))
            return;

        IEnumerable<IAttachment> logSource;
        
        if (userMessage.Reference != null && message.Reference.ReferenceType.IsSpecified && message.Reference.ReferenceType.Value == MessageReferenceType.Forward)
        {
            logSource = userMessage.ForwardedMessages.Count > 0 
                ? userMessage.ForwardedMessages.First().Message.Attachments 
                : [];
        }
        else
        {
            logSource = message.Attachments;
        }

        var logsToAnalize = logSource
            .Where(x => x.Filename.EndsWith(".log") || x.Filename.EndsWith(".txt"))
            .ToArray();
        
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

        var self = guild.GetUser(SocketClient.CurrentUser.Id);

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

    protected async Task<byte[]?> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            var responseTime = Stopwatch.StartNew();
            HttpResponseMessage response = await HttpClient.GetAsync(url, ct);
            Program.Debug(nameof(WrenchManBot), $"Attachment {url} returned {response.StatusCode} in {responseTime.ElapsedMilliseconds:F2}ms");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException e)
        {
            Program.Error(nameof(WrenchManBot), $"Failed to fetch resource from {url}: {e.Message}");
            return null;
        }
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

            Stream stream = new MemoryStream(data);

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

    private async Task<Stream?> ProcessAttachment(Stream attachment, CancellationToken ct)
    {
        var minimumProcessingTime = Task.Delay(250);

        var report = await _logAnalyzer.ProcessLogAsync(attachment, ct);
        await minimumProcessingTime;

        if (report.LikelyInvalid)
        {
            Program.Warn(nameof(WrenchManBot), "Got sent an invalid log file!");
            return null;
        }
        else
        {
            MemoryStream output = new();
            Renderer.WrenchManRender(report, output);
            output.Position = 0;
            return output;
        }
    }
}