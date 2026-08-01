using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.TransformRules;

public partial class Group2Transform : ITransformRuleImpl
{
    [GeneratedRegex("^group2\\((?<key>[a-zA-Z0-9_-]+),\\s*(?<value>[a-zA-Z0-9_-]+)\\)$")]
    private static partial Regex RuleRegex();
    
    private readonly string _key;
    private readonly string _value;

    private Group2Transform(string key, string value)
    {
        _key = key;
        _value = value;
    }

    public static Group2Transform? TryParse(string rule)
    {
        var match = RuleRegex().Match(rule);

        if (!match.Success)
            return null;

        return new Group2Transform(match.Groups["key"].Value, match.Groups["value"].Value);
    }

    public bool Process(Match match, LogLine logLine, LogRule rule, LogReport report)
    {
        if (!ITransformRuleImpl.TypeCheckInit<Dictionary<string, string>>(report, rule.Transform?.Section, out var typedContent))
            return false;
        
        var key = match.Groups.TryGetValue(_key, out var keyGroup) && keyGroup.Success ? keyGroup.Value : null;
        var value = match.Groups.TryGetValue(_value, out var valueGroup) && valueGroup.Success ? valueGroup.Value : null;
        
        if (key != null && value != null)
            typedContent[key] = value;
        else
            return false;

        return true;
    }
}