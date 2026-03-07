namespace BepinexLogAnalysis;

public interface IJob
{
    /// <summary>
    /// Should return true if this job managed to extract some data from the log.
    /// </summary>
    public bool ExtractedAnyData { get; }
    
    /// <summary>
    /// Process custom logic before beginning log analysis, if needed.
    /// </summary>
    /// <param name="context">Current log context with additional data associated with the log</param>
    public void OnLogBegin(LogContext context);
    
    /// <summary>
    /// Process custom logic after ending log analysis, if needed.
    /// </summary>
    /// <param name="context">Current log context with additional data associated with the log</param>
    public void OnLogEnd(LogContext context);

    /// <summary>
    /// Process a log line and extract results from it.
    /// </summary>
    /// <param name="context">Current log context with additional data associated with the log</param>
    /// <param name="line">Extracted log line to analyze</param>
    /// <returns>True to continue processing more jobs after this one, false to skip them (and thus "consume" the log line)</returns>
    public bool ProcessLog(LogContext context, LogLine line);
    
    /// <summary>
    /// Emit information about the extracted log data to the log report
    /// </summary>
    /// <param name="context">Current log context with additional data associated with the log</param>
    /// <param name="stream">Stream to append data to</param>
    public void OutputResults(LogContext context, StreamWriter stream);
    
    /// <summary>
    /// Clear or reset all state of this job.
    /// </summary>
    /// <param name="context">Current log context with additional data associated with the log</param>
    public void Reset(LogContext context);
}
