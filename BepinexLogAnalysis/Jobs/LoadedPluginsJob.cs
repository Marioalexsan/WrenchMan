using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

// Notes: this class both deals with checking for BepInEx plugins,
// and with reporting information about all plugin providers (not just BepInEx ones)

public partial class LoadedPluginsJob : IJob
{
    [GeneratedRegex("""loading \[(.*) ([0-9\.]*)\]""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex LoadedPluginRegex();

    public bool ExtractedAnyData { get; private set; }

    public bool ProcessLog(LogContext context, LogLine line)
    {
        if (line.Source != KnownSources.BepInEx)
            return true;

        Match loadMatch = LoadedPluginRegex().Match(line.Contents);

        if (!loadMatch.Success)
            return true;

        var guid = loadMatch.Groups[1].Value;
        var version = Version.TryParse(loadMatch.Groups[2].Value, out var parsedVersion) ? parsedVersion : new(0, 0, 0);
        context.AddPlugin("BepInEx plugins", guid, version);
        ExtractedAnyData = true;
        
        return false;
    }

    public void OutputResults(LogContext context, StreamWriter stream)
    {
        stream.WriteLine("--- Loaded plugins ---");
        stream.WriteLine();

        if (context.Plugins.Count == 0)
        {
            stream.WriteLine("...no plugins found! Is this a valid log file?");
            stream.WriteLine();
            return;
        }

        foreach (var (provider, plugins) in context.Plugins.OrderBy(x => x.Key))
        {
            var fieldWidth = plugins.Max(x => x.Name.Length);
            
            stream.Write(provider);
            stream.Write(" (");
            stream.Write(plugins.Count);
            stream.WriteLine("):");

            foreach (var plugin in plugins.OrderBy(x => x.Name))
            {
                stream.Write("  ");
                stream.Write(plugin.Name);

                for (int i = plugin.Name.Length; i < fieldWidth; i++)
                    stream.Write(' ');

                if (plugin.Version != null)
                {
                    stream.Write(' ');
                    stream.Write(plugin.Version);
                }
                
                stream.WriteLine();
            }

            stream.WriteLine();
        }
    }

    public void Reset(LogContext context)
    {
        ExtractedAnyData = false;
    }

    public void OnLogBegin(LogContext context)
    {
        // Nothing
    }

    public void OnLogEnd(LogContext context)
    {
        // Nothing
    }
}
