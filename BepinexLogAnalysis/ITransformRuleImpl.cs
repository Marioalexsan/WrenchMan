using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis;

public interface ITransformRuleImpl
{
    public bool Process(Match match, LogLine logLine, LogRule rule, LogReport report);

    protected static bool TypeCheckInit<T>(LogReport report, string? section, [NotNullWhen(true)] out T? value) where T : new()
    {
        if (section == null)
        {
            value = default;
            return false;
        }
        
        if (report.Content.TryGetValue(section, out var content))
        {
            if (content is T typedContent)
            {
                value = typedContent;
                return true;
            }
        }
        else
        {
            report.Content[section] = value = new T();
            return true;
        }
        
        value = default;
        return false;
    }
}