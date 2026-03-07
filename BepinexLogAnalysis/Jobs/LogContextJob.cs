using System.Globalization;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public partial class LogContextJob : IJob
{
    [GeneratedRegex("""bepinex ([0-9\.]*) - (.*) \((.*)\)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex LogStartRegex();

    [GeneratedRegex("""Waking up at (\S*) (\S*) (AM|PM) UTC\.\.\. Game Version is: ([\S\ ]*)""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex HomebreweryWakeup();
    
    private string? _game;
    private string? _gameVersion;
    private string? _bepinexVersion;
    private string? _gameStartTime;

    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _endTime = DateTime.UtcNow;

    public bool ExtractedAnyData => _game != null || _gameVersion != null || _bepinexVersion != null || _gameStartTime != null;

    public bool ProcessLog(LogContext context, LogLine line)
    {
        // It's not guaranteed that this is the first line
        // For example, Player.log can have extra junk at the start before BepInEx logs come in
        if (_game == null && line.Source == KnownSources.BepInEx)
        {
            // Check if we can extract the game and BepInEx version
            Match gameMatch = LogStartRegex().Match(line.Contents);

            if (gameMatch.Success)
            {
                // Set global game context *only if* it's not overriden by configuration
                _bepinexVersion = context.BepInExVersion = gameMatch.Groups[1].Value;
                context.Game ??= _game = gameMatch.Groups[2].Value;
            }

            return false;
        }

        if (context.Game == KnownGames.Atlyss)
        {
            // Homebrewery logs the startup time, which we can use to deduce when the log was taken, and ATLYSS's version
            if (_gameStartTime == null && line.Source == KnownSources.Homebrewery)
            {
                Match wakeupCall = HomebreweryWakeup().Match(line.Contents);

                if (wakeupCall.Success)
                {
                    _gameStartTime = DateTime.TryParse($"{wakeupCall.Groups[1].Value} {wakeupCall.Groups[2].Value} {wakeupCall.Groups[3].Value}", null, DateTimeStyles.AssumeUniversal, out var result)
                        ? result.ToUniversalTime().ToString("yyyy-MM-dd hh:mm:ss UTC")
                        : _gameStartTime;
                    _gameVersion = wakeupCall.Groups[4].Value;

                    return false;
                }
            }
        }

        return true;
    }

    public void OutputResults(LogContext context, StreamWriter stream)
    {
        stream.WriteLine("--- Metadata ---");
        stream.WriteLine();

        stream.Write("Game        ");
        stream.Write(_game ?? "Unknown game");
        stream.Write(", ");
        stream.WriteLine(_gameVersion ?? "unknown version");

        stream.Write("BepInEx     ");
        stream.WriteLine(_bepinexVersion ?? "Unknown version");

        stream.Write("Game start  ");
        stream.WriteLine(_gameStartTime ?? "Unknown");

        stream.Write("Log metrics ");
        stream.Write($"{(context.LogSizeBytes == -1 ? "Unknown " : context.LogSizeBytes / 1048576f):F2}");
        stream.Write(" MiB of data, ");
        stream.Write($"{(_endTime - _startTime).TotalMilliseconds:F1}");
        stream.Write(" ms CPU, ");
        stream.Write($"{(GC.GetTotalMemory(false) / 1048576f):F2}");
        stream.WriteLine(" MiB RAM");

        stream.WriteLine();
    }

    public void Reset(LogContext context)
    {
        _game = null;
        _gameVersion = null;
        _bepinexVersion = null;
        _gameStartTime = null;

        _startTime = DateTime.UtcNow;
        _endTime = DateTime.UtcNow;
    }

    public void OnLogBegin(LogContext context)
    {
        _startTime = DateTime.UtcNow;
    }

    public void OnLogEnd(LogContext context)
    {
        _endTime = DateTime.UtcNow;
    }
}
