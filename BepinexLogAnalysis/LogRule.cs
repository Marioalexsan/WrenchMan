using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BepinexLogAnalysis.TransformRules;

namespace BepinexLogAnalysis;

public class LogRuleList
{
    /// <summary>
    /// A human-readable identifier for this rule list.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Unknown";
    
    /// <summary>
    /// Filter based on currently detected game name. Is evaluated before any individual filters.
    /// </summary>
    [JsonPropertyName("globalGameFilter")]
    public List<string>? GlobalGameFilter { get; set; }
    
    /// <summary>
    /// Filter based on the source of the log line. Is evaluated before any individual filters.
    /// </summary>
    [JsonPropertyName("globalSourceFilter")]
    public List<string>? GlobalSourceFilter { get; set; }

    [JsonPropertyName("rules")]
    public List<LogRule> Rules { get; set; } = [];
}

/// <summary>
/// Defines an evaluation rule for the log analyzer.
/// </summary>
public class LogRule
{
    /// <summary>
    /// Configures how log lines are evaluated and scored for highlighting in the report.
    /// </summary>
    public class ScoringRule
    {
        /// <summary>
        /// If set to true, the log line is removed from the scoring results entirely.
        /// </summary>
        [JsonPropertyName("ignore")]
        public bool Ignore { get; set; }
        
        /// <summary>
        /// Modifies the priority of the log line within its level by the given value.
        /// </summary>
        [JsonPropertyName("severity")]
        public int Severity { get; set; }
    }
    
    /// <summary>
    /// Configures how log lines are transformed into more detailed data blocks within the report.
    /// </summary>
    public class TransformRule
    {
        /// <summary>
        /// The section to put data in.
        /// </summary>
        [JsonPropertyName("section")]
        public string Section { get; set; } = "";
        
        /// <summary>
        /// The data rule to use, based on custom rule syntax.
        /// </summary>
        [JsonPropertyName("rule")]
        public string Rule { get; set; } = "";
    }
    
    /// <summary>
    /// Rules that set this to true are ignored.
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
    
    /// <summary>
    /// Short description for what this rule does.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Pattern for matching log lines based on their content.
    /// </summary>
    [JsonPropertyName("contentPattern")]
    [JsonConverter(typeof(LogRuleRegexConverter))]
    public Regex ContentPattern { get; set; } = new(".*");

    /// <summary>
    /// Filter based on currently detected game name.
    /// </summary>
    [JsonPropertyName("gameFilter")]
    public List<string>? GameFilter { get; set; }

    /// <summary>
    /// Filter based on the source of the log line.
    /// </summary>
    [JsonPropertyName("sourceFilter")]
    public List<string>? SourceFilter { get; set; }

    /// <summary>
    /// Specifies how to score this log line and highlight it as part of the results.
    /// </summary>
    [JsonPropertyName("scoring")]
    public ScoringRule? Scoring { get; set; }

    /// <summary>
    /// Specifies how to transform this log line into information and print it as part of the results.
    /// </summary>
    [JsonPropertyName("transform")]
    public TransformRule? Transform
    {
        get => field;
        set
        {
            field = value;
            TransformImpl = null;
        }
    }

    [JsonIgnore]
    public ITransformRuleImpl? TransformImpl
    {
        get
        {
            if (Transform == null)
                return null;

            if (field != null)
                return field;

            if ((field = TableTransform.TryParse(Transform.Rule)) != null)
                return field;

            if ((field = Group2Transform.TryParse(Transform.Rule)) != null)
                return field;

            if ((field = Group3Transform.TryParse(Transform.Rule)) != null)
                return field;

            if ((field = DumpLineTransform.TryParse(Transform.Rule)) != null)
                return field;

            if ((field = SetFieldsTransform.TryParse(Transform.Rule)) != null)
                return field;

            return field = new InvalidRule();
        }
        private set => field = value;
    }
}