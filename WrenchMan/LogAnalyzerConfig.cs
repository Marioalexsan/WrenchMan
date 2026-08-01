namespace WrenchMan;

public class LogAnalyzerConfig
{
    /// <summary>
    /// A list of files to load scoring matchers from.
    /// </summary>
    public List<string> BuiltinRulesToUse { get; set; } = [
        "core",
        "basic_scoring",
        "ATLYSS/atlyss",
        "ATLYSS/homebrewery",
        "ATLYSS/custom_quests",
    ];
    
    /// <summary>
    /// A list of files to load additional rules from.
    /// </summary>
    public List<string> AdditionalRulePaths { get; set; } = [];
}