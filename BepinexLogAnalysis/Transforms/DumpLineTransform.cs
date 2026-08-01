using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.TransformRules;

public partial class DumpLineTransform : ITransformRuleImpl
{
    [GeneratedRegex("^dump_line$")]
    private static partial Regex RuleRegex();
    
    private DumpLineTransform() { }

    public static DumpLineTransform? TryParse(string rule)
    {
        var match = RuleRegex().Match(rule);

        if (!match.Success)
            return null;

        return new DumpLineTransform();
    }

    public bool Process(Match match, LogLine logLine, LogRule rule, LogReport report)
    {
        if (!ITransformRuleImpl.TypeCheckInit<List<string>>(report, rule.Transform?.Section, out var typedContent))
            return false;

        typedContent.Add($"{logLine.LogLevel} - {logLine.Source} - {logLine.Contents}");
        return true;
    }
}