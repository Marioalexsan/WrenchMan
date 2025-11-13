using BepinexLogAnalysis.SeverityRules;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public enum MatcherTarget
{
    Source,
    Contents
}

public record struct SeverityRule(Regex Matcher, float Score, MatcherTarget Target = MatcherTarget.Contents);

public partial class TopIssuesJob : IJob
{
    public const int MaxTopIssues = 20;
    public const float MinimumScore = 1f;

    private readonly List<(LogLine Line, float Score)> _scoredMessages = [];
    private readonly HashSet<int> _processedMessages = [];

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

    public void OutputResults(StreamWriter stream)
    {
        var topIssues = _scoredMessages
            .Where(x => x.Score > MinimumScore)
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
            stream.Write(Line.Source);
            stream.Write(" - ");
            stream.Write(Line.LogLevel);
            stream.Write(" (");
            stream.Write(Score);
            stream.Write(") ");
            stream.WriteLine(Line.Source);
            stream.Write("Content: ");
            stream.WriteLine(Line.Contents);
            stream.WriteLine();
        }

        stream.WriteLine();
    }

    public void Reset()
    {
        _scoredMessages.Clear();
        _processedMessages.Clear();
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
