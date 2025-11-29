using BepinexLogAnalysis.SeverityRules;
using System.Data;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public partial class MapLoaderJob(TopIssuesJob topIssuesJob) : IJob
{
    [GeneratedRegex("""(?:Unable to read header from archive file:|Failed to load asset bundle:|Failed to read data for the AssetBundle) .*\.manifest\.mapbundle""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex TriedToReadAManifest();

    private readonly TopIssuesJob _topIssuesJob = topIssuesJob;

    public void ProcessLog(LogLine line, Dictionary<string, string> context)
    {
        if (line.Source != KnownSources.MapLoader && line.Source != KnownSources.UnityLog)
            return;

        if (!context.TryGetValue("game", out var game) && game != KnownGames.Atlyss)
            return;

        Match failOnManifest = TriedToReadAManifest().Match(line.Contents);

        if (failOnManifest.Success)
        {
            _topIssuesJob.RemoveLineFromScoring(line);
            return;
        }
    }

    public void OutputResults(StreamWriter stream)
    {
        // Nothing for now
    }

    public void Reset()
    {
        // Nothing
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
