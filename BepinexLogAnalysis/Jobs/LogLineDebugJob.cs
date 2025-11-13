namespace BepinexLogAnalysis.Jobs;

public partial class LogLineDebugJob : IJob
{
    private readonly List<LogLine> _logLines = [];

    public void ProcessLog(LogLine line, Dictionary<string, string> context)
    {
        _logLines.Add(line);
    }

    public void OutputResults(StreamWriter stream)
    {
        stream.WriteLine("--- Log debug ---");

        foreach (var line in _logLines)
        {
            stream.Write("[#");
            stream.Write(line.Line);
            stream.Write("] ");
            stream.WriteLine(line.Source);
            stream.WriteLine(line.Contents);
            stream.WriteLine();
        }

        stream.WriteLine();
    }

    public void Reset()
    {
        _logLines.Clear();
    }

    public void OnLogBegin()
    {
        // Nothing
    }

    public void OnLogEnd()
    {
        // Nothing
    }
}
