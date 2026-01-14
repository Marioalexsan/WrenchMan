using BepinexLogAnalysis;
using Discord;
using Discord.WebSocket;
using System.Text;
using System.Text.Json;

namespace WrenchMan;

internal class WrenchManBot : IDisposable
{
    private const GatewayIntents Intents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents;

    protected readonly DiscordSocketClient SocketClient = new(new DiscordSocketConfig
    {
        MessageCacheSize = 100,
        LogLevel = LogSeverity.Info,
        AlwaysDownloadUsers = true,
        GatewayIntents = Intents,
    });

    public static string BasePath { get; } = Directory.GetCurrentDirectory();
    public static string ConfigPath { get; } = Path.Combine(BasePath, "config");
    public static string GlobalConfigPath { get; } = Path.Combine(ConfigPath, "wrenchman.json");
    public static string GuildConfigsFolderPath { get; } = Path.Combine(ConfigPath, "guilds");
    public static string GuildConfigPath(string guildId) => Path.Combine(GuildConfigsFolderPath, $"{guildId}.json");

    private readonly HttpClient HttpClient = new();

    private readonly WrenchConfig _config;
    private readonly Dictionary<string, GuildSettings> _guildConfigs = [];

    private GuildSettings GetConfigForGuild(string guildId)
    {
        if (!_guildConfigs.TryGetValue(guildId, out var config))
        {
            _guildConfigs[guildId] = config = new GuildSettings();
            File.WriteAllText(GuildConfigPath(guildId), JsonSerializer.Serialize(config));
            Console.WriteLine($"Initialized new guild config for guild {guildId}");
        }

        return config;
    }

    public WrenchManBot()
    {
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

        string? token = Environment.GetEnvironmentVariable("WRENCHMAN_AUTH");

        if (token == null && File.Exists(_config.TokenFilePath))
        {
            token = File.ReadAllText(_config.TokenFilePath);
        }

        if (token == null)
        {
            Console.WriteLine("Couldn't find token!");
            return;
        }
        
        foreach (var file in Directory.EnumerateFiles(GuildConfigsFolderPath, "*.json"))
        {
            var guildId = Path.GetFileNameWithoutExtension(file);
            var guildSettings = JsonSerializer.Deserialize<GuildSettings>(File.ReadAllText(file)) ?? throw new NullReferenceException("Guild config was null");
            _guildConfigs[guildId] = guildSettings;
            Console.WriteLine($"Loaded guild config settings for guild {guildId}");
        }

        SocketClient.GuildAvailable += GuildAvailable;
        SocketClient.GuildUnavailable += GuildUnavailable;
        SocketClient.Log += SocketLog;

        SocketClient.MessageReceived += OnMessageReceived;
        SocketClient.SlashCommandExecuted += OnSlashCommand;
        SocketClient.Connected += OnConnected;

        SocketClient.LoginAsync(TokenType.Bot, token);
        SocketClient.StartAsync();
    }

    public void Dispose()
    {
        SocketClient?.Dispose();
        HttpClient?.Dispose();
    }

    private async Task OnConnected()
    {
        Console.WriteLine("Connected!");
    }

    private async Task OnSlashCommand(SocketSlashCommand command)
    {
        await command.RespondAsync("I don't have commands yet!", ephemeral: true);
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
            var settings = GetConfigForGuild(guildChannel.Guild.Id.ToString()).LogAnalyzer;

            if (settings.WhitelistedChannels.Count > 0)
            {
                if (!settings.WhitelistedChannels.Contains(guildChannel.Id.ToString()))
                    return;
            }
            else if (settings.BlacklistedChannels.Count > 0)
            {
                if (settings.BlacklistedChannels.Contains(guildChannel.Id.ToString()))
                    return;
            }
            else
            {
                if (!settings.LookInThreads && message.Channel is SocketThreadChannel)
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

        foreach (var url in fileUrls)
            tasks.Add(FetchAsync(url));

        var attachments = await Task.WhenAll(tasks);

        Console.WriteLine($"Processing {attachments.Length} logs ({totalSize / 1024} KiB) from {message.Author.Username} in channel {message.Channel.Name} ({message.Channel.Id})...");

        for (int i = 0; i < attachments.Length; i++)
        {
            var data = attachments[i];

            if (data == null)
                continue;

            Console.WriteLine($"Analyzing attachment {fileNames[i]}...");

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
            await ProcessAttachment(message, fileNames[i], stream);
        }
    }

    private async Task SocketLog(LogMessage message)
    {
        Console.WriteLine(message.ToString());
    }

    private async Task GuildUnavailable(SocketGuild guild)
    {

    }

    private async Task GuildAvailable(SocketGuild guild)
    {

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
            Console.WriteLine($"Failed to fetch resource from {url}");
            return null;
        }
    }

    private async Task ProcessAttachment(SocketMessage message, string fileName, Stream attachment)
    {
        var minimumProcessingTime = Task.Delay(750);

        MemoryStream output = new();
        bool success = await LogAnalyzer.ProcessLogAsync(attachment, output, CancellationToken.None);
        await minimumProcessingTime;

        if (!success)
        {
            Console.WriteLine($"File {fileName} doesn't seem like it's a log, skipping it.");
        }
        else
        {
            output.Position = 0;
            await message.Channel.SendFileAsync(output, "Report.txt", "Here's a summary of your log file!");
            Console.WriteLine($"Sent summary for {fileName}.");
        }
    }
}
