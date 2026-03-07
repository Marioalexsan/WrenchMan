namespace BepinexLogAnalysis.Jobs.ScoringJob;

public enum MatcherTarget
{
    Source,
    Contents
}

/// <summary>
/// Generic configurable job that ranks messages based on importance.
/// Usually treated as last job in terms of priority.
/// </summary>
public class ScoringJob : IJob
{
    public ScoringJob(IEnumerable<ScoringJobRuleList> ruleLists)
    {
        _ruleLists = ruleLists.ToArray();
    }
    
    public const int MaxTopIssues = 20;
    public const float MinimumScore = 1f;

    private readonly ScoringJobRuleList[] _ruleLists;
    private readonly List<(LogLine Line, float Score)> _scoredMessages = [];
    private readonly HashSet<int> _processedMessages = [];

    public bool ExtractedAnyData => _scoredMessages.Count > 0;

    public bool ProcessLog(LogContext context, LogLine line)
    {
        if (!_processedMessages.Add(line.GetDuplicateHashcode()))
            return true;

        float score = line.LogLevel switch
        {
            "Warning" => 5,
            "Error" => 10,
            "Fatal" => 20,
            _ => 0
        };

        var game = context.Game;

        foreach (var ruleList in _ruleLists)
        {
            if (game != null && ruleList.ApplicableGames.Count > 0 && !ruleList.ApplicableGames.Contains(game))
                continue;

            foreach (var rule in ruleList.Rules)
            {
                if (rule.ContentPattern != null && !rule.ContentPattern.IsMatch(line.Contents))
                    continue;

                score += rule.Score;
            }
        }

        _scoredMessages.Add(new()
        {
            Line = line,
            Score = score
        });
        return true;
    }

    public void OutputResults(LogContext context, StreamWriter stream)
    {
        var topIssues = _scoredMessages
            .Where(x => x.Score > MinimumScore)
            .OrderByDescending(x => x.Score)
            .Take(MaxTopIssues);

        stream.WriteLine($"--- Top Issues (showing max {MaxTopIssues}) ---");
        stream.WriteLine();

        bool gotAtLeastOne = false;

        foreach (var (Line, Score) in topIssues)
        {
            gotAtLeastOne = true;
            
            const bool newFormat = false;

            if (newFormat)
            {
                // TODO: Write a different way to log issues
            }
            else
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
        }
        
        if (!gotAtLeastOne)
            stream.WriteLine($"...Couldn't find anything important!");
        
        stream.WriteLine();
    }

    public void Reset(LogContext context)
    {
        _scoredMessages.Clear();
        _processedMessages.Clear();
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
