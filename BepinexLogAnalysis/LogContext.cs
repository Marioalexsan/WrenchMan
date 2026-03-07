namespace BepinexLogAnalysis;

public class LogContext
{
    public string? Game { get; set; }
    public string? BepInExVersion { get; set; }
    public long LogSizeBytes { get; set; } = -1;

    public List<LogLine> AllLines { get; } = [];

    public Dictionary<string, List<(string Name, Version? Version)>> Plugins = [];

    public void AddPlugin(string pluginProvider, string plugin, Version? version)
    {
        if (!Plugins.TryGetValue(pluginProvider, out var list))
            list = Plugins[pluginProvider] = new List<(string Name, Version? Version)>();

        list.Add((plugin, version));
    }
}
