namespace BepinexLogAnalysis;

public class LogReport
{
    // Log metadata
    public string? Game { get; set; }
    public string? GameVersion { get; set; }
    public string? BepInExVersion { get; set; }
    public bool LikelyInvalid => 
        Content.Count <= 1 && 
        (Game == null || BepInExVersion == null) && 
        ScoredMessages.Count == 0;

    public List<string> ProcessingErrors { get; set; } = [];

    public List<(LogLine Line, int Score)> ScoredMessages { get; set; } = [];

    public SortedDictionary<string, object> Content { get; set; } = [];
}
