namespace WrenchMan;

/// <summary>
/// Log analyzer per-guild settings.
/// </summary>
public class LogAnalyzerGuildSettings
{
    /// <summary>
    /// Channel IDs in which to search for logs and reply automatically, without using an explicit command.
    /// </summary>
    public List<string> ChannelsToMonitor { get; set; } = [];
}

/// <summary>
/// Stores per-guild settings.
/// </summary>
public class GuildSettings
{
    /// <summary>
    /// If true, the usage of this bot is disallowed in the given guild.
    /// The bot will also attempt to remove itself from them.
    /// </summary>
    public bool Blacklisted { get; set; } = false;
    
    /// <summary>
    /// Settings specific to the log analyzer functionality.
    /// </summary>
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
    public string BepInExLogAnalysisRootConfigPath { get; set; } = "config/bepinex_log_analysis.json";

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
    /// Whenever to use privileged intents and features that require them.
    /// For example, reading logs directly from messages in servers requires the message content intent.
    /// </summary>
    public bool UsePrivilegedIntents { get; set; } = false;

    /// <summary>
    /// List of guild IDs that are allowed to invite the bot.
    /// If not empty, the bot will try to leave guilds which invite it and are not whitelisted.
    /// This only applies to new guilds; existing guilds will be unaffected.
    /// </summary>
    public string[] GuildWhitelist { get; set; } = [];

    /// <summary>
    /// List of user IDs that act as global administrators for the bot.
    /// Global admins can both run commands that modify guild configuratons, as well as global configurations for the bot.
    /// </summary>
    public string[] GlobalAdministrators { get; set; } = [];

    /// <summary>
    /// Whenver to log the user, server and platform when logs are received.
    /// </summary>
    public bool LogUserAndLocationDetails { get; set; } = true;

    /// <summary>
    /// Global settings to use for the bot.
    /// </summary>
    public GlobalSettings Settings { get; set; } = new();
}
