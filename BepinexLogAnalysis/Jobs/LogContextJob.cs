using BepinexLogAnalysis.SeverityRules;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public partial class LogContextJob : IJob
{
    [GeneratedRegex("""bepinex ([0-9\.]*) - (.*) \((.*)\)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex LogStartRegex();

    private string _game = "Unknown";
    private string _gameVersion = "Unknown";

    private string _bepinexVersion = "Unknown";

    private string _gameStartTime = "Unknown";

    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _endTime = DateTime.UtcNow;

    public void ProcessLog(LogLine line, Dictionary<string, string> context)
    {
        if (line.Line == 1 && line.Source == KnownSources.BepInEx)
        {
            // Check if we can extract the game and BepInEx version
            Match gameMatch = LogStartRegex().Match(line.Contents);

            if (gameMatch.Success)
            {
                _bepinexVersion = context["bepinex_version"] = gameMatch.Groups[1].Value;
                _game = context["game"] = gameMatch.Groups[2].Value;
            }

            return;
        }

        if (_game == KnownGames.Atlyss)
        {
            // Homebrewery logs the startup time, which we can use to deduce when the log was taken
            if (_gameStartTime == "Unknown" && line.Source == KnownSources.Homebrewery)
            {
                Match wakeupCall = AtlyssMatchers.HomebreweryWakeup().Match(line.Contents);

                if (wakeupCall.Success)
                {
                    _gameStartTime = DateTime.TryParse($"{wakeupCall.Groups[1].Value} {wakeupCall.Groups[2].Value} {wakeupCall.Groups[3].Value}", null, DateTimeStyles.AssumeUniversal, out var result)
                        ? result.ToUniversalTime().ToString("yyyy-MM-dd hh:mm:ss UTC")
                        : _gameStartTime;
                    _gameVersion = wakeupCall.Groups[4].Value;
                }
            }
        }
    }

    public void OutputResults(StreamWriter stream)
    {
        stream.WriteLine("--- Metadata ---");
        stream.WriteLine();

        stream.Write("Game              ");
        stream.WriteLine(_game);
        stream.Write("Game version      ");
        stream.WriteLine(_gameVersion);

        stream.Write("BepInEx version   ");
        stream.WriteLine(_bepinexVersion);

        stream.Write("Log timestamp     ");
        stream.WriteLine(_gameStartTime);

        stream.Write("Log processed in  ");
        stream.Write((_endTime - _startTime).TotalMilliseconds);
        stream.WriteLine("ms");

        stream.WriteLine();
    }

    public void Reset()
    {
        _game = "Unknown";
        _gameVersion = "Unknown";
        _bepinexVersion = "Unknown";

        _gameStartTime = "Unknown";

        _startTime = DateTime.UtcNow;
        _endTime = DateTime.UtcNow;
    }

    public void OnLogBegin()
    {
        _startTime = DateTime.UtcNow;
    }

    public void OnLogEnd()
    {
        _endTime = DateTime.UtcNow;
    }
}
