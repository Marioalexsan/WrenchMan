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

    private readonly WrenchConfig _config;
    private readonly Dictionary<string, GuildSettings> _guildConfigs = [];
    private readonly LogAnalyzerConfig _logAnalyzerConfig;

    private readonly LogAnalyzer _logAnalyzer;

    private GuildSettings GetConfigForGuild(string guildId)
    {
        if (!_guildConfigs.TryGetValue(guildId, out var config))
        {
            _guildConfigs[guildId] = config = new GuildSettings();
            File.WriteAllText(GuildConfigPath(guildId), JsonSerializer.Serialize(config));
            Program.Debug(nameof(WrenchManBot), $"Initialized new guild config for guild {guildId}");
        }

        return config;
    }

    public WrenchManBot()
    {
        Program.Info(nameof(WrenchManBot), "Started bot!");
        
        if (!Directory.Exists(ConfigPath))
            Directory.CreateDirectory(ConfigPath);

        if (!Directory.Exists(GuildConfigsFolderPath))
            Directory.CreateDirectory(GuildConfigsFolderPath);

        if (!File.Exists(GlobalConfigPath))
        {
            File.WriteAllText(GlobalConfigPath, JsonSerializer.Serialize(_config = new()));
        }
        else
        {
            _config = JsonSerializer.Deserialize<WrenchConfig>(File.ReadAllText(GlobalConfigPath)) ?? throw new NullReferenceException("Config was null");
        }

        foreach (var file in Directory.EnumerateFiles(GuildConfigsFolderPath, "*.json"))
        {
            var guildId = Path.GetFileNameWithoutExtension(file);
            var guildSettings = JsonSerializer.Deserialize<GuildSettings>(File.ReadAllText(file)) ?? throw new NullReferenceException("Guild config was null");
            _guildConfigs[guildId] = guildSettings;
            Program.Debug(nameof(WrenchManBot), $"Loaded guild config settings for guild {guildId}");
        }

        if (!File.Exists(_config.Settings.BepInExLogAnalysisRootConfigPath))
        {
            var matchersPath = Path.Combine(ConfigPath, "scoring_job_matchers");

            if (!Directory.Exists(matchersPath))
                Directory.CreateDirectory(matchersPath);

            File.WriteAllText(_config.Settings.BepInExLogAnalysisRootConfigPath, JsonSerializer.Serialize(_logAnalyzerConfig = new()
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
            _logAnalyzerConfig = JsonSerializer.Deserialize<LogAnalyzerConfig>(File.ReadAllText(_config.Settings.BepInExLogAnalysisRootConfigPath)) ?? throw new NullReferenceException("Log analysis config was null");
        }

        _logAnalyzer = new LogAnalyzer(new LogAnalyzerOptions()
        {
            Config = _logAnalyzerConfig,
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

        if (token == null && File.Exists(_config.TokenFilePath))
        {
            token = File.ReadAllText(_config.TokenFilePath);
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
            switch (cmd.Data.Name)
            {
                case "analyze-log":
                    var attachment = (IAttachment)cmd.Data.Options.First().Value;

                    if (attachment.Size >= 1024 * 1024 * 20)
                    {
                        await cmd.FollowupAsync("Sorry, I can only parse logs that have a total size of at most 20 MiB!", ephemeral: true);
                        return;
                    }

                    var responseTime = Stopwatch.StartNew();
                    var data = await FetchAsync(attachment.Url);

                    if (data == null)
                    {
                        await cmd.FollowupAsync("Failed to get the attachment file! (cc: <@320578056488222723>)", allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
                        return;
                    }

                    Program.Info(nameof(WrenchManBot), $"Analyzing attachment {attachment.Filename}...");

                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
                    var result = await ProcessAttachment(stream);

                    stream.Position = 0;

                    if (result == null)
                    {
                        Program.Info(nameof(WrenchManBot), $"File {attachment.Filename} doesn't seem like it's a log.");
                        await cmd.FollowupAsync("That doesn't look like a valid log file! It should be a Player.log, or LogOutput.log file");
                    }
                    else
                    {
                        var sanitizeTime = Stopwatch.StartNew();
                        var sanitizedLog = Sanitizer.Sanitize(stream);
                        Program.Debug(nameof(WrenchManBot), $"Sanitization took {sanitizeTime.ElapsedMilliseconds:F2}ms");

                        await cmd.FollowupWithFilesAsync([
                            new FileAttachment(sanitizedLog, "SanitizedLog.txt"),
                            new FileAttachment(result, "Report.txt")
                        ], "Here's a summary of your log file!");
                        Program.Info(nameof(WrenchManBot), $"Sent summary for {attachment.Filename}.");
                    }

                    Program.Debug(nameof(WrenchManBot), $"Response in total took {responseTime.ElapsedMilliseconds:F2}ms");
                    break;
                default:
                    await cmd.FollowupAsync("Uh, I don't know how to process that command...", ephemeral: true);
                    break;
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
        var analyzeLogCommand = new SlashCommandBuilder()
            .WithName("analyze-log")
            .WithDescription("Extracts logs from arguments or a given message, and analyzes them.")
            .AddOption("file", ApplicationCommandOptionType.Attachment, "Attach a log file to analyze.", isRequired: true)
            .Build();

        await SocketClient!.BulkOverwriteGlobalApplicationCommandsAsync([
            analyzeLogCommand
        ]);
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

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot || message.Author.IsWebhook || message.Source == MessageSource.System)
            return;

        var channelType = message.Channel.GetChannelType();

        if (channelType == ChannelType.DM && !_config.Settings.LogAnalyzer.LookInDirectMessages)
            return;

        if (message.Channel is SocketGuildChannel guildChannel)
        {
            var config = GetConfigForGuild(guildChannel.Guild.Id.ToString()).LogAnalyzer;

            if (config.WhitelistedChannels.Count > 0)
            {
                if (!config.WhitelistedChannels.Contains(guildChannel.Id.ToString()))
                    return;
            }
            else if (config.BlacklistedChannels.Count > 0)
            {
                if (config.BlacklistedChannels.Contains(guildChannel.Id.ToString()))
                    return;
            }
            else
            {
                if (!config.LookInThreads && message.Channel is SocketThreadChannel)
                    return;
            }
        }

        List<Task<string?>> tasks = [];
        List<string> fileUrls = [];
        List<string> fileNames = [];

        int totalSize = 0;

        foreach (var item in message.Attachments)
        {
            if (!(item.Filename.EndsWith(".log") || item.Filename.EndsWith(".txt")))
                continue;

            totalSize += item.Size;

            fileUrls.Add(item.Url);
            fileNames.Add(item.Filename);
        }

        if (fileUrls.Count == 0)
            return;

        if (totalSize >= 1024 * 1024 * 20)
        {
            await message.Channel.SendMessageAsync("Sorry, I can only parse logs that have a total size of at most 20 MiB!");
            return;
        }
        
        var responseTime = Stopwatch.StartNew();

        foreach (var url in fileUrls)
            tasks.Add(FetchAsync(url));

        var attachments = await Task.WhenAll(tasks);

        if (_config.LogUserAndLocationDetails)
        {
            var messageAuthorInfo = $"{message.Author.Username} ({message.Author.Id})";

            var messageLocationInfo = message.Channel switch
            {
                IGuildChannel fromGuild => $"{message.Channel.Name} ({message.Channel.Id}), {fromGuild.Guild.Name} ({fromGuild.Guild.Id})",
                IDMChannel _ => $"direct messages",
                _ => $"unknown DM / server ({message.Channel.GetType()})"
            };

            Program.Info(nameof(WrenchManBot), $"Processing {attachments.Length} logs ({totalSize / 1024} KiB) from {messageAuthorInfo} in {messageLocationInfo}...");
        }
        else
        {
            Program.Info(nameof(WrenchManBot), $"Processing {attachments.Length} logs ({totalSize / 1024} KiB) from a request...");
        }

        for (int i = 0; i < attachments.Length; i++)
        {
            var data = attachments[i];

            if (data == null)
                continue;

            Program.Info(nameof(WrenchManBot), $"Analyzing attachment {fileNames[i]}...");

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));

            var result = await ProcessAttachment(stream);

            if (result == null)
            {
                Program.Info(nameof(WrenchManBot), $"File {fileNames[i]} doesn't seem like it's a log, skipping it.");
            }
            else
            {
                await message.Channel.SendFileAsync(result, "Report.txt", "Here's a summary of your log file!");
                Program.Info(nameof(WrenchManBot), $"Sent summary for {fileNames[i]}.");
            }
        }
        
        Program.Debug(nameof(WrenchManBot), $"Response in total took {responseTime.ElapsedMilliseconds:F2}ms");
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
        
        if (_config.GuildWhitelist.Length > 0 && !_config.GuildWhitelist.Contains(guild.Id.ToString()))
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

    protected async Task<string?> FetchAsync(string url)
    {
        try
        {
            HttpResponseMessage response = await HttpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException)
        {
            Program.Error(nameof(WrenchManBot), $"Failed to fetch resource from {url}");
            return null;
        }
    }

    private async Task<Stream?> ProcessAttachment(Stream attachment)
    {
        var minimumProcessingTime = Task.Delay(750);

        MemoryStream output = new();

        bool success = await _logAnalyzer.ProcessLogAsync(attachment, output, CancellationToken.None);
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