using System.Text.RegularExpressions;

namespace BepinexLogAnalysis;

public class LogAnalyzerOptions
{
    /// <summary>
    /// Rule lists to use.
    /// </summary>
    public List<LogRuleList> RuleLists { get; set; } = [];

    /// <summary>
    /// How many top scored issues to return.
    /// </summary>
    public int MaxScoredIssues { get; set; } = 20;
}

public partial class LogAnalyzer
{
    private readonly LogAnalyzerOptions _options;
    
    public LogAnalyzer(LogAnalyzerOptions options)
    {
        _options = options;
    }

    // TODO: No idea if this regex is evil or not
    [GeneratedRegex("""(?:.*?)\[(debug|info|warning|message|fatal|error)\s*:\s*([^\]]+)\]\s?(.*?)(?=(?:\r?\n\[|\z))""", RegexOptions.IgnoreCase | RegexOptions.Singleline, 1000)]
    private static partial Regex LogLineRegex();

    public async Task<LogReport> ProcessLogAsync(Stream input, CancellationToken ct = default)
    {
        _rulesWithErrors.Clear();
        _processedMessages.Clear();
        _scoredMessages.Clear();
        
        var logLineRegex = LogLineRegex();

        using var reader = new StreamReader(input, leaveOpen: true);
        var log = await reader.ReadToEndAsync(ct);

        int logLinesSoFar = 0;

        var metadata = new Dictionary<string, string>()
        {
            ["log_size_bytes"] = input.Length.ToString(),
            ["processing_start"] = DateTime.UtcNow.ToString("O"),
            ["memory_start"] = GC.GetAllocatedBytesForCurrentThread().ToString(),
        };

        var report = new LogReport()
        {
            Content =
            {
                ["Metadata"] = metadata
            }
        };
        
        foreach (Match match in logLineRegex.Matches(log))
        {
            var logLine = new LogLine(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value.Trim(), ++logLinesSoFar);

            Evaluate(logLine, report, _options.RuleLists);
        }

        report.ScoredMessages.AddRange(_scoredMessages
            .Where(x => x.Score >= 1)
            .OrderByDescending(x => x.Score)
            .Take(_options.MaxScoredIssues));

        metadata["processing_end"] = DateTime.UtcNow.ToString("O");
        metadata["memory_end"] = GC.GetAllocatedBytesForCurrentThread().ToString();

        return report;
    }

    private readonly List<(LogLine Line, int Score)> _scoredMessages = [];
    private readonly HashSet<int> _processedMessages = [];
    private readonly HashSet<LogRule> _rulesWithErrors = [];

    private void Evaluate(LogLine line, LogReport report, List<LogRuleList> ruleLists)
    {
        if (_processedMessages.Add(line.GetDuplicateHashcode()))
        {
            int severity = line.LogLevel switch
            {
                "Warning" => 5,
                "Error" => 10,
                "Fatal" => 20,
                _ => 0
            };

            var game = report.Game;
            bool severityIgnored = false;
            
            foreach (var (ruleListIndex, ruleList) in ruleLists.Index())
            {
                if (ruleList.GlobalGameFilter != null && (game == null || !ruleList.GlobalGameFilter.Contains(game)))
                    continue;
            
                if (ruleList.GlobalSourceFilter != null && !ruleList.GlobalSourceFilter.Contains(line.Source))
                    continue;

                foreach (var (ruleIndex, rule) in ruleList.Rules.Index())
                {
                    if (rule.GameFilter != null && (game == null || !rule.GameFilter.Contains(game)))
                        continue;
            
                    if (rule.SourceFilter != null && !rule.SourceFilter.Contains(line.Source))
                        continue;

                    var match = rule.ContentPattern.Match(line.Contents);
                    
                    if (!match.Success)
                        continue;
                        
                    if (!severityIgnored && rule.Scoring != null)
                    {
                        severityIgnored = rule.Scoring.Ignore;
                        severity += rule.Scoring.Severity;
                    }

                    if (rule.TransformImpl != null)
                    {
                        if (!rule.TransformImpl.Process(match, line, rule, report) && _rulesWithErrors.Add(rule))
                        {
                            report.ProcessingErrors.Add(
                                $"Rule {ruleIndex} in rule list {ruleListIndex} failed to process its transform."
                            );
                        }
                    }
                }
            }

            if (!severityIgnored)
                _scoredMessages.Add((line, severity));
        }
    }
}
