using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.TransformRules;

public class InvalidRule : ITransformRuleImpl
{
    public bool Configure(string rule)
    {
        return true;
    }

    public bool Process(Match match, LogLine logLine, LogRule rule, LogReport report)
    {
        return false;
    }
}