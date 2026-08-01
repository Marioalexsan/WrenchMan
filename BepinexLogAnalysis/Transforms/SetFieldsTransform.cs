using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.TransformRules;

public partial class SetFieldsTransform : ITransformRuleImpl
{
    [GeneratedRegex("^set_fields$")]
    private static partial Regex RuleRegex();

    private SetFieldsTransform() { }

    public static SetFieldsTransform? TryParse(string rule)
    {
        var match = RuleRegex().Match(rule);

        if (!match.Success)
            return null;

        return new SetFieldsTransform();
    }

    public bool Process(Match match, LogLine logLine, LogRule rule, LogReport report)
    {
        if (!ITransformRuleImpl.TypeCheckInit<Dictionary<string, string>>(report, rule.Transform?.Section, out var typedContent))
            return false;

        foreach (Group groupMatch in match.Groups)
        {
            // Do not grab numbered groups here
            if (!int.TryParse(groupMatch.Name, out _))
            {
                typedContent[groupMatch.Name] = groupMatch.Value;

                // Special treatment for some fields
                switch (groupMatch.Name)
                {
                    case "game":
                        report.Game = groupMatch.Value;
                        break;
                    case "game_version":
                        report.GameVersion = groupMatch.Value;
                        break;
                    case "bepinex_version":
                        report.BepInExVersion = groupMatch.Value;
                        break;
                }
            }
        }
        
        return true;
    }
}