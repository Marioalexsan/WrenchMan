namespace BepinexLogAnalysis;

public record struct LogLine(
    string LogLevel,
    string Source,
    string Contents,
    int Line
)
{
    public int GetDuplicateHashcode() => HashCode.Combine(LogLevel, Source, Contents);
}
