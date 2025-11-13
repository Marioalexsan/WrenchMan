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
    public static string GlobalConfig { get; } = Path.Combine(ConfigPath, "wrenchman.json");

    private readonly HttpClient HttpClient = new();

    private readonly HashSet<ulong> _channels = [
        1395556467083575447,
        1395556505285296249,
        1395563056096084039,
        1395563126216462336,
        1395549831254245459,
        1395562799878635640,
        1399069219902984384
    ];

    private readonly WrenchConfig _config;

    public WrenchManBot()
    {
        if (!Directory.Exists(ConfigPath))
            Directory.CreateDirectory(ConfigPath);

        if (!File.Exists(GlobalConfig))
        {
            File.WriteAllText(GlobalConfig, JsonSerializer.Serialize(_config = new()));
        }
        else
        {
            _config = JsonSerializer.Deserialize<WrenchConfig>(File.ReadAllText(GlobalConfig)) ?? throw new NullReferenceException("Config was null");
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

        if (!_channels.Contains(message.Channel.Id))
            return;

        Console.WriteLine("Checking for attachments...");

        List<Task<string?>> tasks = [];

        foreach (var item in message.Attachments)
        {
            if (item.Filename.EndsWith(".log") || item.Filename.EndsWith(".txt"))
            {
                tasks.Add(FetchAsync(item.Url));
            }
        }

        if (tasks.Count == 0)
            return;

        var attachments = await Task.WhenAll(tasks);

        Console.WriteLine($"Processing {attachments.Length} attachments...");

        foreach (var data in attachments)
        {
            if (data == null)
                continue;

            Console.WriteLine("Analyzing log...");

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
            await ProcessAttachment(message, stream);
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

    private async Task ProcessAttachment(SocketMessage message, Stream attachment)
    {
        await message.Channel.SendMessageAsync("I see you posted a log file, let me summarize it for you!");

        var minimumProcessingTime = Task.Delay(750);

        MemoryStream output = new();
        await LogAnalyzer.ProcessLogAsync(attachment, output, CancellationToken.None);
        await minimumProcessingTime;

        output.Position = 0;
        await message.Channel.SendFileAsync(output, "Report.txt", "Here you go!");
    }
}
