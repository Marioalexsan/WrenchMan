namespace BepinexLogAnalysis;

public interface IJob
{
    public void OnLogBegin();
    public void OnLogEnd();

    public void ProcessLog(LogLine line, Dictionary<string, string> context);
    public void OutputResults(StreamWriter stream);
    public void Reset();
}
