using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public partial class BepinexLoadedPluginsJob : IJob
{
    [GeneratedRegex("""loading \[(.*) ([0-9\.]*)\]""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex LoadedPluginRegex();

    private readonly Dictionary<string, Version> _loadedPlugins = [];

    public bool HasAnyPlugins => _loadedPlugins.Count > 0;

    public void ProcessLog(LogLine line, Dictionary<string, string> context)
    {
        if (line.Source != KnownSources.BepInEx)
            return;

        Match loadMatch = LoadedPluginRegex().Match(line.Contents);

        if (!loadMatch.Success)
            return;

        var guid = loadMatch.Groups[1].Value;
        var version = Version.TryParse(loadMatch.Groups[2].Value, out var parsedVersion) ? parsedVersion : new(0, 0, 0);
        _loadedPlugins[guid] = version;
    }

    public void OutputResults(StreamWriter stream)
    {
        stream.WriteLine("--- Loaded plugins ---");
        stream.WriteLine();

        if (_loadedPlugins.Count == 0)
        {
            stream.WriteLine("...no plugins found! Is this a valid log file?");
            stream.WriteLine();
            return;
        }

        var fieldWidth = _loadedPlugins.Max(x => x.Key.Length);

        foreach (var plugin in _loadedPlugins.OrderBy(x => x.Key))
        {
            stream.Write(plugin.Key);

            for (int i = plugin.Key.Length; i < fieldWidth; i++)
                stream.Write(' ');
            
            stream.Write(' ');
            stream.Write(plugin.Value);
            stream.WriteLine();
        }

        stream.WriteLine();
    }

    public void Reset()
    {
        _loadedPlugins.Clear();
    }

    public void OnLogBegin()
    {
        // Nothing
    }

    public void OnLogEnd()
    {
        // Nothing
    }
}
