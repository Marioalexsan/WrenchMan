using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.TransformRules;

public partial class TableTransform : ITransformRuleImpl
{
    [GeneratedRegex("^table\\((?:(?<patternName>[a-zA-Z0-9_-]+)+(,\\s*)?)+\\)$")]
    private static partial Regex RuleRegex();

    private List<string> _rows = [];
    
    private TableTransform() { }

    public static TableTransform? TryParse(string rule)
    {
        var match = RuleRegex().Match(rule);

        if (!match.Success)
            return null;
        
        return new TableTransform()
        {
            _rows = match.Groups["patternName"].Captures.Select(x => x.Value).ToList()
        };
    }

    public bool Process(Match match, LogLine logLine, LogRule rule, LogReport report)
    {
        if (!ITransformRuleImpl.TypeCheckInit<List<List<string>>>(report, rule.Transform?.Section, out var typedContent))
            return false;

        List<string> data = [];

        foreach (var row in _rows)
        {
            var cell = match.Groups.GetValueOrDefault(row);
            data.Add(cell?.Value ?? "");
        }
        
        typedContent.Add(data);
        return true;
    }
}