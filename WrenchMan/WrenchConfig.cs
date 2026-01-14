namespace WrenchMan;

/// <summary>
/// Log analyzer per-guild settings.
/// </summary>
public class LogAnalyzerGuildSettings
{
    /// <summary>
    /// Whenever to search in threads for logs to parse, true by default.
    /// </summary>
    public bool LookInThreads { get; set; } = false;
    
    /// <summary>
    /// Channel IDs in which to search for logs. Leave empty to search in all channels.
    /// If channels are specified for both this and <see cref="BlacklistedChannels"/>, the whitelist takes priority.
    /// </summary>
    public List<string> WhitelistedChannels { get; set; } = [];

    /// <summary>
    /// Channel IDs to ignore when searching for logs. Leave empty to search in all channels.
    /// If channels are specified for both this and <see cref="WhitelistedChannels"/>, the whitelist takes priority.
    /// </summary>
    public List<string> BlacklistedChannels { get; set; } = [];
}

/// <summary>
/// Stores per-guild settings.
/// </summary>
public class GuildSettings
{
    public LogAnalyzerGuildSettings LogAnalyzer { get; set; } = new();
}

/// <summary>
/// Log analyzer global settings.
/// </summary>
public class LogAnalyzerGlobalSettings
{
    /// <summary>
    /// Whenever to reply to users sending logs in Direct Messages, false by default.
    /// </summary>
    public bool LookInDirectMessages { get; set; } = false;
}

/// <summary>
/// Global settings for stuff.
/// </summary>
public class GlobalSettings
{
    public LogAnalyzerGlobalSettings LogAnalyzer { get; set; } = new();
}

/// <summary>
/// Bot configuration.
/// </summary>
public class WrenchConfig
{
    /// <summary>
    /// The path to the file that contains the Discord bot token to use.
    /// </summary>
    public string TokenFilePath { get; set; } = ".wrenchman_token";

    /// <summary>
    /// Global settings to use for the bot.
    /// </summary>
    public GlobalSettings Settings { get; set; } = new();
}
