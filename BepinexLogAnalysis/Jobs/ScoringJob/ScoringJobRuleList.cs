using System.Text.Json;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs.ScoringJob;

public class ScoringJobRule
{
    public string? Description { get; set; }
    public Regex? ContentPattern { get; set; }
    public int Score { get; set; }
}

public class ScoringJobRuleList
{
    private class SerializedRule
    {
        public string? Description { get; set; }
        public string? ContentPattern { get; set; }
        public int Score { get; set; }
    }

    private class SerializedList
    {
        public List<string> ApplicableGames { get; set; } = [];
        public List<SerializedRule> Rules { get; set; } = [];
        public Dictionary<string, int> ScoringPerLogLevel { get; set; } = [];
    }

    public List<string> ApplicableGames { get; set; } = [];
    public List<ScoringJobRule> Rules { get; set; } = [];

    public static ScoringJobRuleList LoadFromStream(Stream stream, Action<string>? warnLog)
    {
        var config = new ScoringJobRuleList();

        try
        {
            var document = JsonSerializer.Deserialize<SerializedList>(stream, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (document == null)
                throw new InvalidDataException("Configuration deserialization returned null");

            config.ApplicableGames.AddRange(document.ApplicableGames);

            foreach (var rule in document.Rules)
            {
                if (rule.ContentPattern == null)
                {
                    warnLog?.Invoke("Encountered a matcher with no actionable operations!");
                    continue;
                }

                try
                {
                    config.Rules.Add(new ScoringJobRule()
                    {
                        ContentPattern = rule.ContentPattern != null
                            ? new Regex(rule.ContentPattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000))
                            : null,
                        Description = rule.Description,
                        Score = rule.Score
                    });
                }
                catch (Exception e)
                {
                    warnLog?.Invoke("Failed to load a matcher from a configuration.");
                    warnLog?.Invoke($"Exception: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            warnLog?.Invoke("Failed to load configuration for Top Issues job.");
            warnLog?.Invoke($"Exception: {e.Message}");
        }

        return config;
    }
}