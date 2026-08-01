using System.Reflection;
using System.Text.Json;

namespace BepinexLogAnalysis;

public static class BundledRules
{
    private const string Prefix = "bundled_rules/";
    
    public static Dictionary<string, LogRuleList> All
    {
        get
        {
            if (field != null)
                return field;

            field = [];
            
            var assembly = Assembly.GetExecutingAssembly();
        
            var names = assembly
                .GetManifestResourceNames()
                .Where(x => x.StartsWith(Prefix));

            foreach (var name in names)
            {
                using var stream = assembly.GetManifestResourceStream(name)
                                   ?? throw new InvalidOperationException("Failed to get bundled rule list");
                field.Add(name.Substring(Prefix.Length), JsonSerializer.Deserialize<LogRuleList>(stream)
                                ?? throw new InvalidOperationException("Failed to get bundled rule list"));
            }

            return field;
        }
    }

    public static LogRuleList Core => All["core"];
    public static LogRuleList BasicScoring => All["basic_scoring"];
    public static LogRuleList[] Atlyss =>
    [
        All["ATLYSS/atlyss"],
        All["ATLYSS/homebrewery"],
        All["ATLYSS/custom_quests"],
    ];
}