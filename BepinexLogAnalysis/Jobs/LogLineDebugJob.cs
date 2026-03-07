namespace BepinexLogAnalysis.Jobs;

public class LogLineDebugJob : IJob
{
    private readonly List<LogLine> _logLines = [];

    public bool ExtractedAnyData => false; // Ignore this job for purposes of log file detection

    public bool ProcessLog(LogContext context, LogLine line)
    {
        _logLines.Add(line);
        return true;
    }

    public void OutputResults(LogContext context, StreamWriter stream)
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

    public void Reset(LogContext context)
    {
        _logLines.Clear();
    }

    public void OnLogBegin(LogContext context)
    {
        // Nothing
    }

    public void OnLogEnd(LogContext context)
    {
        // Nothing
    }
}
