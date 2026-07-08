using System.Text.Json;
using BepinexLogAnalysis.Jobs;
using System.Text.RegularExpressions;
using BepinexLogAnalysis.Jobs.ScoringJob;

namespace BepinexLogAnalysis;

public class LogAnalyzerConfig
{
    /// <summary>
    /// The game to analyze logs for.
    /// If set to null, the analyzer will try to auto-detect the game from the logs.
    /// </summary>
    public string? GameToAnalyze { get; set; } = null;

    /// <summary>
    /// Whenever to enable the log debug job.
    /// </summary>
    public bool EnableLogDebugJob { get; set; } = false;

    /// <summary>
    /// A list of files to load scoring matchers from.
    /// </summary>
    public List<string> ScoringMatcherPaths { get; set; } = [];

    /// <summary>
    /// A list of inline scoring matchers to use.
    /// </summary>
    public List<ScoringJobRuleList> InlineScoringMatchers { get; set; } = [];
}

public class LogAnalyzerOptions
{
    public LogAnalyzerConfig Config { get; set; } = new();
    
    public Action<LogLevel, string>? LogMethod { get; set; }
}

public partial class LogAnalyzer
{
    private readonly LogAnalyzerOptions _options;
    private IJob[] _jobs;
    
    public LogAnalyzer(LogAnalyzerOptions options)
    {
        _options = options;

        List<ScoringJobRuleList> scoringJobRuleLists = [..options.Config.InlineScoringMatchers];

        foreach (var filePath in _options.Config.ScoringMatcherPaths)
        {
            if (!File.Exists(filePath))
            {
                _options.LogMethod?.Invoke(LogLevel.Warn, $"Skipping path {filePath}: matcher file does not exist.");
                continue;
            }

            try
            {
                using var stream = File.OpenRead(filePath);

                var ruleList = ScoringJobRuleList.LoadFromStream(stream, (message) => _options.LogMethod?.Invoke(LogLevel.Warn, message));
                scoringJobRuleLists.Add(ruleList);
                
                _options.LogMethod?.Invoke(LogLevel.Info, $"Loaded {ruleList.Rules.Count} matchers from {filePath} {(ruleList.ApplicableGames.Count == 0 ? "for all games" : $"for the following games: {string.Join(", ", ruleList.ApplicableGames)}")}.");
            }
            catch (Exception e)
            {
                _options.LogMethod?.Invoke(LogLevel.Warn, $"Failed to load {filePath}: matcher file threw an exception on load: {e}");
            }
        }
        
        List<IJob> pipeline =
        [
            new LogContextJob(),
            new LoadedPluginsJob(),
            new HomebreweryJob(),
            new CustomQuestsJob(),
            new ScoringJob(scoringJobRuleLists)
        ];
        
        if (_options.Config.EnableLogDebugJob)
            pipeline.Add(new LogLineDebugJob());

        _jobs = pipeline.ToArray();
    }

    // TODO: No idea if this regex is evil or not
    [GeneratedRegex("""\[(debug|info|warning|message|fatal|error)\s*:\s*([^\]]+)\]\s?(.*?)(?=(?:\r?\n\[|\z))""", RegexOptions.IgnoreCase | RegexOptions.Singleline, 1000)]
    private static partial Regex LogLineRegex();

    public async Task<bool> ProcessLogAsync(Stream input, Stream output, CancellationToken ct)
    {
        var logLineRegex = LogLineRegex();

        using var reader = new StreamReader(input, leaveOpen: true);
        var log = await reader.ReadToEndAsync(ct);

        int logLinesSoFar = 0;

        var context = new LogContext()
        {
            LogSizeBytes = reader.BaseStream.Length
        };
        
        foreach (var job in _jobs)
            job.Reset(context);

        foreach (var job in _jobs)
            job.OnLogBegin(context);

        foreach (Match match in logLineRegex.Matches(log))
        {
            var logLine = new LogLine(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value.Trim(), ++logLinesSoFar);

            foreach (var job in _jobs)
            {
                if (!job.ProcessLog(context, logLine))
                    break;
            }
        }

        foreach (var job in _jobs)
            job.OnLogEnd(context);

        if (!_jobs.Any(x => x.ExtractedAnyData))
            return false;

        using var writer = new StreamWriter(output, leaveOpen: true);

        foreach (var job in _jobs)
            job.OutputResults(context, writer);

        await writer.FlushAsync(ct);
        return true;
    }
}
