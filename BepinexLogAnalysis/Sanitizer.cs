using System.Text.RegularExpressions;

namespace BepinexLogAnalysis;

public static partial class Sanitizer
{
    // D:\Users\Mario\
    [GeneratedRegex("""[A-Z]:(?:\\|\/)Users(?:\\|\/)([^\\\/]+)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex UserFolderRegexWindows();
    
    // /home/Mario/
    [GeneratedRegex("""/home/([^/]+)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex UserFolderRegexLinux();
    
    // https://developer.valvesoftware.com/wiki/SteamID
    [GeneratedRegex("""STEAM_[0-9]+:[0-9]+:([0-9]+)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex SteamIdRegex();
    
    // https://developer.valvesoftware.com/wiki/SteamID
    [GeneratedRegex("""[a-zA-Z]:[01]:([0-9])+""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex SteamId3Regex();
    
    // https://developer.valvesoftware.com/wiki/SteamID
    // User SteamID accounts likely start with the sequence 0x01100001, which would
    // theoretically result in a 64 bit number between 76561197960265728 and 76561202255233023.
    // To keep things simple, let's just sanitize any 17 digit numbers that start with "76561".
    [GeneratedRegex("""76561([0-9]{12})""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex Steam64IdIndividualAccountRegex();

    private static readonly List<Regex> Matchers =
    [
        UserFolderRegexWindows(),
        UserFolderRegexLinux(),
        SteamIdRegex(),
        SteamId3Regex(),
        Steam64IdIndividualAccountRegex(),
    ];
    
    public static Stream Sanitize(Stream input)
    {
        var reader = new StreamReader(input, leaveOpen: true);
        var data = reader.ReadToEnd();
        
        static string MatchSanitizer(Match match)
        {
            var matchText = match.Groups[0].Value;
            var startIndex = match.Groups[1].Index - match.Groups[0].Index;
            return matchText.Remove(startIndex, match.Groups[1].Length).Insert(startIndex, "[*****]");
        }

        foreach (var matcher in Matchers)
            data = matcher.Replace(data, MatchSanitizer);
        
        var memoryStream = new MemoryStream();

        var writer = new StreamWriter(memoryStream, leaveOpen: true);
        writer.Write(data);
        memoryStream.Position = 0;

        return memoryStream;
    }
}
