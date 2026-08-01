using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis;

public class LogRuleRegexConverter : JsonConverter<Regex>
{
    private const RegexOptions RegexOpts = RegexOptions.IgnoreCase | RegexOptions.Compiled;
    
    public override Regex Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!, RegexOpts, TimeSpan.FromMilliseconds(1000));

    public override void Write(Utf8JsonWriter writer, Regex regex, JsonSerializerOptions options) =>
        writer.WriteStringValue(regex.ToString());
}