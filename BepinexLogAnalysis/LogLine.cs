namespace BepinexLogAnalysis;

public record struct LogLine(
    BIELogLevel LogLevel,
    string Source,
    string Contents,
    int Line
)
{
    public int GetDuplicateHashcode() => HashCode.Combine(LogLevel, Source, Contents);

    public override string ToString() => $"{Line,4} | [{Line,-7}:{Source,10}] {Contents}";
}
