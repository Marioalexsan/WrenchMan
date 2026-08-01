using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.TransformRules;

public partial class Group3Transform : ITransformRuleImpl
{
    [GeneratedRegex("^group3\\((?<key1>[a-zA-Z0-9_-]+),\\s*(?<key2>[a-zA-Z0-9_-]+),\\s*(?<value>[a-zA-Z0-9_-]+)\\)$")]
    private static partial Regex RuleRegex();
    
    private readonly string _key1;
    private readonly string _key2;
    private readonly string _value;

    private Group3Transform(string key1, string key2, string value)
    {
        _key1 = key1;
        _key2 = key2;
        _value = value;
    }

    public static Group3Transform? TryParse(string rule)
    {
        var match = RuleRegex().Match(rule);

        if (!match.Success)
            return null;

        return new Group3Transform(match.Groups["key1"].Value, match.Groups["key2"].Value, match.Groups["value"].Value);
    }

    public bool Process(Match match, LogLine logLine, LogRule rule, LogReport report)
    {
        if (!ITransformRuleImpl.TypeCheckInit<Dictionary<string, Dictionary<string, string>>>(report, rule.Transform?.Section, out var typedContent))
            return false;
        
        var key1 = match.Groups.TryGetValue(_key1, out var key1Group) && key1Group.Success ? key1Group.Value : null;
        var key2 = match.Groups.TryGetValue(_key2, out var key2Group) && key2Group.Success ? key2Group.Value : null;
        var value = match.Groups.TryGetValue(_value, out var valueGroup) && valueGroup.Success ? valueGroup.Value : null;

        if (key1 != null && key2 != null && value != null)
        {
            if (!typedContent.TryGetValue(key1, out var inner))
                inner = typedContent[key1] = [];

            inner[key2] = value;
        }
        else return false;

        return true;
    }
}