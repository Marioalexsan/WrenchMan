using System.Diagnostics;
using System.Globalization;
using System.Text;
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

    public async Task<LogReport> ProcessLogAsync(Stream input, CancellationToken ct = default)
    {
        _rulesWithErrors.Clear();
        _processedMessages.Clear();
        _scoredMessages.Clear();
        
        var metadata = new Dictionary<string, string>()
        {
            ["log_size_bytes"] = input.Length.ToString(),
        };
        
        var totalTime = new Stopwatch();
        var totalMemory = new Memorywatch();
        
        totalMemory.ReferencePoint();
        totalTime.Start();

        int logLinesSoFar = 0;

        var report = new LogReport()
        {
            Content =
            {
                ["Metadata"] = metadata
            }
        };
        
        using var reader = new StreamReader(input, leaveOpen: true);
        var content = new StringBuilder(8192);
        var source = new StringBuilder(64);
        var logLevel = BIELogLevel.Unknown;
        var startLineIndex = 0;
        
        SplitLines(reader, (line, lineIndex, reachedEnd) =>
        {
            // Currently discarding any prefixes
            // It's not clear to me what AsyncLogger does, and if it's standard across modding communities
            if (ExtractBepinexHeader(line, out var nextLevel, out var nextSource, out _, out var after))
            {
                if (content.Length > 0)
                {
                    // Flush current log entry
                    ProcessBepinexEntry(report, startLineIndex, logLevel, source.ToString(), "", content.ToString());
                    content.Clear();
                    source.Clear();
                }

                content.Append(after);
                source.Append(nextSource);
                startLineIndex = lineIndex;
                logLevel = nextLevel;
            }
            else
            {
                if (content.Length > 0)
                    content.Append('\n');
                content.Append(line);
            }

            if (reachedEnd)
            {
                // Flush last log entry forcefully
                ProcessBepinexEntry(report, startLineIndex, logLevel, source.ToString(), "", content.ToString());
            }
        });
        
        report.ScoredMessages.AddRange(_scoredMessages
            .Where(x => x.Score >= 1)
            .OrderByDescending(x => x.Score)
            .Take(_options.MaxScoredIssues));

        
        totalTime.Stop();
        totalMemory.Measure();
        
        metadata["processing_time"] = totalTime.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        metadata["memory_alloc"] = totalMemory.AllocatedBytes.ToString();

        return report;
    }

    private readonly List<(LogLine Line, int Score)> _scoredMessages = [];
    private readonly HashSet<int> _processedMessages = [];
    private readonly HashSet<LogRule> _rulesWithErrors = [];

    [GeneratedRegex("""\[(Debug  |Info   |Warning|Message|Fatal  |Error  )\:[^\]]*]""")]
    private static partial Regex BepinexHeader();

    private bool ExtractBepinexHeader(
        ReadOnlySpan<char> line,
        out BIELogLevel level,
        out ReadOnlySpan<char> source,
        out ReadOnlySpan<char> before,
        out ReadOnlySpan<char> after)
    {
        var matches = BepinexHeader().EnumerateMatches(line);
        if (!matches.MoveNext())
        {
            level = BIELogLevel.Unknown;
            source = ReadOnlySpan<char>.Empty;
            before = ReadOnlySpan<char>.Empty;
            after = ReadOnlySpan<char>.Empty;
            return false;
        }

        var match = matches.Current;

        level = Enum.TryParse<BIELogLevel>(line.Slice(match.Index + 1, 7).Trim(), false, out var theLevel)
            ? theLevel
            : BIELogLevel.Unknown;
        source = line[(match.Index + 9)..(match.Index + match.Length - 1)].Trim();
        before = line[..match.Index].TrimEnd();
        after = line[(match.Index + match.Length)..].TrimStart();
        return true;
    }

    private void SplitLines(StreamReader reader, Action<ReadOnlySpan<char>, int, bool> callback)
    {
        Span<char> buffer = stackalloc char[65536];
        int bufferCount = 0;
        int lineIndex = 0;
        
        bool dropLine = false;
        
        while (true)
        {
            var availableBuffer = buffer[bufferCount..];
            int charsRead = reader.ReadBlock(availableBuffer);
            bufferCount += charsRead;
            var currentContent = buffer[..bufferCount];

            int nextNewline;
            while ((nextNewline = currentContent.IndexOf('\n')) != -1)
            {
                var line = currentContent[..nextNewline];
                currentContent = currentContent[(nextNewline + 1)..];
                bufferCount -= line.Length + 1;

                if (!dropLine)
                {
                    if (line.Length > 0 && line[^1] == '\r')
                        line = line[..^1];
                    
                    callback(line, lineIndex, false);
                }
                else dropLine = false;

                lineIndex++;
            }
            
            currentContent.CopyTo(buffer);

            if (bufferCount == buffer.Length)
            {
                // Too many characters in current line
                dropLine = true;
                bufferCount = 0;
            }

            if (charsRead == 0)
            {
                callback(buffer[..bufferCount], lineIndex, true);
                break;
            }
        }
    }

    private void ProcessBepinexEntry(
        LogReport report,
        int startLineIndex,
        BIELogLevel logLevel,
        string source,
        string prefix,
        string content
        )
    {
        var logLine = new LogLine(
            logLevel,
            source,
            content,
            startLineIndex + 1);
        
        Evaluate(logLine, report, _options.RuleLists);
    }

    private void Evaluate(LogLine line, LogReport report, List<LogRuleList> ruleLists)
    {
        if (_processedMessages.Add(line.GetDuplicateHashcode()))
        {
            int severity = line.LogLevel switch
            {
                BIELogLevel.Warning => 5,
                BIELogLevel.Error => 10,
                BIELogLevel.Fatal => 20,
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
