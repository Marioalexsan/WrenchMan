using System.Text.RegularExpressions;

namespace BepinexLogAnalysis;

public static partial class Sanitizer
{
    // D:\Users\Mario\
    [GeneratedRegex("""[A-Z]:(?:\\|\/)Users(?:\\|\/)([^\\\/]+)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex UserFolderRegex();
    
    public static Stream Sanitize(Stream input)
    {
        var reader = new StreamReader(input, leaveOpen: true);
        var data = reader.ReadToEnd();
        
        var output = UserFolderRegex().Replace(data, match =>
        {
            var matchText = match.Groups[0].Value;
            var startIndex = match.Groups[1].Index - match.Groups[0].Index;
            return matchText.Remove(startIndex, match.Groups[1].Length).Insert(startIndex, "[***]");
        });

        var memoryStream = new MemoryStream();

        var writer = new StreamWriter(memoryStream, leaveOpen: true);
        writer.Write(output);

        return memoryStream;
    }
}