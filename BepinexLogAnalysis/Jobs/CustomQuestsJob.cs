using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public partial class CustomQuestsJob : IJob
{
    [GeneratedRegex("""Quest "(.*)" not found!""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex QuestNotFound();

    [GeneratedRegex("""Error while loading quest "(.*)" - Quest NOT loaded!""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex QuestFailedToLoad();

    private readonly List<string> _questsFailedToLoad = [];
    private readonly List<string> _questsNotFound = [];
    private bool _encounteredLogLines = false;
    private bool _encounteredIssues = false;

    public bool ExtractedAnyData => _encounteredIssues;

    public bool ProcessLog(LogContext context, LogLine line)
    {
        if (line.Source != KnownSources.CustomQuests)
            return true;

        if (context.Game != KnownGames.Atlyss)
            return true;

        _encounteredLogLines = true;

        Match questFailedToLoad = QuestFailedToLoad().Match(line.Contents);

        if (questFailedToLoad.Success)
        {
            _questsFailedToLoad.Add(questFailedToLoad.Groups[1].Value);
            _encounteredIssues = true;
            return false;
        }

        Match questNotFound = QuestNotFound().Match(line.Contents);

        if (questNotFound.Success)
        {
            _questsNotFound.Add(questNotFound.Groups[1].Value);
            _encounteredIssues = true;
            return false;
        }

        return true;
    }

    public void OutputResults(LogContext context, StreamWriter stream)
    {
        if (!_encounteredLogLines)
            return;

        stream.WriteLine("--- Custom Quests Issues ---");
        stream.WriteLine();

        if (!_encounteredIssues)
        {
            stream.WriteLine("...no issues found!");
            stream.WriteLine();
            return;
        }

        if (_questsFailedToLoad.Count > 0)
        {
            _questsFailedToLoad.Sort();

            stream.WriteLine("Quests that failed to load:");

            foreach (var objName in _questsFailedToLoad)
            {
                stream.Write("  ");
                stream.WriteLine(objName);
            }

            stream.WriteLine();
        }

        if (_questsNotFound.Count > 0)
        {
            _questsNotFound.Sort();

            stream.WriteLine("Quests that were not found:");

            foreach (var objName in _questsNotFound)
            {
                stream.Write("  ");
                stream.WriteLine(objName);
            }

            stream.WriteLine();
        }
    }

    public void Reset(LogContext context)
    {
        _questsNotFound.Clear();
        _encounteredLogLines = false;
        _encounteredIssues = false;
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
