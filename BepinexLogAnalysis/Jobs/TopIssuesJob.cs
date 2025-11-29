using BepinexLogAnalysis.SeverityRules;
using System.Linq;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public enum MatcherTarget
{
    Source,
    Contents
}

public record struct SeverityRule(Regex Matcher, float Score, MatcherTarget Target = MatcherTarget.Contents, string? ExpectedSource = null);

public partial class TopIssuesJob : IJob
{
    public const int MaxTopIssues = 20;
    public const float MinimumScore = 1f;

    private readonly List<(LogLine Line, float Score)> _scoredMessages = [];
    private readonly HashSet<int> _processedMessages = [];
    private readonly HashSet<int> _bannedMessages = [];

    public void ProcessLog(LogLine line, Dictionary<string, string> context)
    {
        if (!_processedMessages.Add(HashCode.Combine(line.Source, line.Contents)))
            return;

        float score = line.LogLevel switch
        {
            "Warning" => 5,
            "Error" => 10,
            "Fatal" => 20,
            _ => 0
        };

        score += ProcessRules(line, GenericMatchers.All);
        score += ProcessRules(line, GetGameSpecificMatcher(context.GetValueOrDefault("game")));

        _scoredMessages.Add(new()
        {
            Line = line,
            Score = score
        });
    }

    public void RemoveLineFromScoring(LogLine line)
    {
        var hashCode = HashCode.Combine(line.Source, line.Contents);
        _processedMessages.Add(hashCode);
        _bannedMessages.Add(hashCode);
    }

    public void OutputResults(StreamWriter stream)
    {
        var topIssues = _scoredMessages
            .Where(x => x.Score > MinimumScore && !_bannedMessages.Contains(HashCode.Combine(x.Line.Source, x.Line.Contents)))
            .OrderByDescending(x => x.Score)
            .Take(MaxTopIssues);

        stream.WriteLine($"--- Top Issues (showing max {MaxTopIssues}) ---");
        stream.WriteLine();

        if (!topIssues.Any())
        {
            stream.WriteLine($"...Couldn't find anything important!");
            return;
        }

        foreach (var (Line, Score) in topIssues)
        {
            stream.Write("  ");
            stream.Write(Line.Source);
            stream.Write(" - ");
            stream.Write(Line.LogLevel);
            stream.Write(" (");
            stream.Write(Score);
            stream.Write(") Line #");
            stream.WriteLine(Line.Line);
            stream.WriteLine(Line.Contents.Trim().Replace("\n", "\n  "));
            stream.WriteLine();
        }

        stream.WriteLine();
    }

    public void Reset()
    {
        _scoredMessages.Clear();
        _processedMessages.Clear();
        _bannedMessages.Clear();
    }

    public void OnLogBegin()
    {
        // Nothing
    }

    public void OnLogEnd()
    {
        // Nothing
    }

    private static float ProcessRules(LogLine line, SeverityRule[] rules)
    {
        float score = 0f;

        foreach (var rule in rules)
        {
            if (rule.ExpectedSource != null && rule.ExpectedSource != line.Source)
                continue;

            if (rule.Matcher.Match(rule.Target == MatcherTarget.Contents ? line.Contents : line.Source).Success)
                score += rule.Score;
        }

        return score;
    }

    private static SeverityRule[] GetGameSpecificMatcher(string? game) => game switch
    {
        "ATLYSS" => AtlyssMatchers.All,
        _ => []
    };
}
