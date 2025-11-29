using BepinexLogAnalysis.Jobs;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis;

public static partial class LogAnalyzer
{
    private static IJob[] CreatePipeline()
    {
        TopIssuesJob topIssuesJob;

        return [
            new LogContextJob(),
            new BepinexLoadedPluginsJob(),
            topIssuesJob = new TopIssuesJob(),
            new HomebreweryJob(topIssuesJob),
            new CustomQuestsJob(topIssuesJob),
            new MapLoaderJob(topIssuesJob),
            //new LogLineDebugJob(),
        ];
    }

    // TODO: No idea if this regex is evil or not
    [GeneratedRegex("""\[(debug|info|warning|message|fatal|error)\s*:\s*([^\]]+)\]\s?(.*?)(?=(?:\n\[|\z))""", RegexOptions.IgnoreCase | RegexOptions.Singleline, 1000)]
    private static partial Regex LogLineRegex();

    public static async Task ProcessLogAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;

        var pipeline = CreatePipeline();

        var logLineRegex = LogLineRegex();

        using var reader = new StreamReader(input, leaveOpen: true);
        var log = await reader.ReadToEndAsync(cancellationToken);

        int logLinesSoFar = 0;

        var context = new Dictionary<string, string>
        {
            ["game"] = "???",
            ["bepinex_version"] = "0.0.0",
        };

        foreach (var job in pipeline)
            job.OnLogBegin();

        foreach (Match match in logLineRegex.Matches(log))
        {
            var logLine = new LogLine(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, ++logLinesSoFar);

            foreach (var job in pipeline)
                job.ProcessLog(logLine, context);
        }

        foreach (var job in pipeline)
            job.OnLogEnd();

        using var writer = new StreamWriter(output, leaveOpen: true);

        foreach (var job in pipeline)
            job.OutputResults(writer);

        await writer.FlushAsync(cancellationToken);
    }
}
