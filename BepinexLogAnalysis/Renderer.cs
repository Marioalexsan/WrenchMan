namespace BepinexLogAnalysis;

public static class Renderer
{
    public static void WrenchManRender(LogReport report, Stream stream)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);

        if (report.Content.TryGetValue("Metadata", out var metadata) && metadata is Dictionary<string, string> theMeta)
        {
            writer.WriteLine("--- Metadata ---");
            writer.WriteLine();

            writer.Write("Game        ");
            writer.Write(theMeta.GetValueOrDefault("game", "Unknown game"));
            writer.Write(", ");
            writer.WriteLine(theMeta.GetValueOrDefault("game_version", "Unknown version"));

            writer.Write("BepInEx     ");
            writer.WriteLine(theMeta.GetValueOrDefault("bepinex_version", "Unknown version"));

            var logSize = long.TryParse(theMeta.GetValueOrDefault("log_size_bytes", ""), out var size) ? size : -1;
            var memAlloc = long.TryParse(theMeta.GetValueOrDefault("memory_alloc", ""), out var malloc) ? malloc : -1;
            var msProcessing = double.TryParse(theMeta.GetValueOrDefault("processing_time", ""), out var pt) ? pt : -1;
            
            writer.Write("Log metrics ");
            writer.Write(logSize <= 0 ? "Unknown " : $"{logSize / 1048576f:F2}");
            writer.Write(" MiB log, ");
            writer.Write($"{msProcessing:F1}");
            writer.Write(" ms, ");
            writer.Write($"{memAlloc / 1048576f:F2}");
            writer.WriteLine(" MiB memory");

            writer.WriteLine();
            
            RenderGroup2(writer, theMeta.Without([
                "game",
                "game_version",
                "bepinex_version",
                "log_size_bytes",
                "processing_time",
                "memory_alloc",
            ]).ToDictionary());
        }

        var plugins = report.Content.Keys.Where(x => x.StartsWith("Plugins/")).Order().ToList();

        foreach (var pluginSection in plugins)
        {
            if (report.Content.TryGetValue(pluginSection, out var pluginList) &&
                pluginList is List<List<string>> thePlugins)
            {
                writer.WriteLine($"--- Loaded {pluginSection.Substring("Plugins/".Length)} plugins ---");
                writer.WriteLine();
                
                RenderTable(writer, thePlugins);
                
                writer.WriteLine();
            }
        }

        foreach (var (sectionName, section) in report.Content.Without(["Metadata", .. plugins]))
        {
            writer.WriteLine($"--- {sectionName} ---");
            writer.WriteLine();
            
            switch (section)
            {
                case Dictionary<string, string> group2:
                    RenderGroup2(writer, group2);
                    break;
                case Dictionary<string, Dictionary<string, string>> group3:
                    RenderGroup3(writer, group3);
                    break;
                case List<List<string>> table:
                    RenderTable(writer, table);
                    break;
                case List<string> list:
                    RenderList(writer, list);
                    break;
                default:
                    writer.Write("...no info could be rendered for this section...");
                    break;
            }
            
            writer.WriteLine();
        }
        
        RenderScoring(writer, report.ScoredMessages);

        if (report.ProcessingErrors.Any())
        {
            writer.WriteLine($"--- Encountered some processing errors while working on this log! ---");
            
            RenderList(writer, report.ProcessingErrors);
        }
    }
    
    // Experimenting a bit
    public static void PawsyRender(LogReport report, Stream stream)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);

        foreach (var (line, score) in report.ScoredMessages.Take(10))
        {
            writer.Write($$"""
                           --- ISSUE ---
                           Line Number #{{line.Line}}
                           Source: {{line.Source}}
                           Severity: {{line.LogLevel}} ({{score}})

                           Contents:

                           """);

            writer.Write(line.Contents.AsSpan(0, Math.Min(line.Contents.Length, 1000)));
            writer.WriteLine();
            writer.WriteLine();
        }
    }
    
    private static void RenderGroup2(StreamWriter writer, Dictionary<string, string> group2)
    {
        foreach (var (key, value) in group2)
        {
            writer.Write("  ");
            writer.Write(key);
            writer.Write(' ');
            writer.WriteLine(value);
        }
    }

    private static void RenderGroup3(StreamWriter writer, Dictionary<string, Dictionary<string, string>> group3)
    {
        foreach (var (key1, pair) in group3)
        {
            writer.Write("  ");
            writer.WriteLine(key1);
            
            foreach (var (key2, value) in pair)
            {
                writer.Write("    ");
                writer.Write(key2);
                writer.Write(' ');
                writer.WriteLine(value);
            }
            
            writer.WriteLine();
        }
    }

    private static void RenderTable(StreamWriter writer, List<List<string>> table)
    {
        var columnSizes = new int[table.Select(row => row.Count).Max()];

        foreach (var row in table)
        {
            foreach (var (index, column) in row.Index())
                columnSizes[index] = Math.Max(columnSizes[index], column.Length);
        }
        
        foreach (var row in table)
        {
            writer.Write("  ");
            
            foreach (var (index, column) in row.Index())
            {
                writer.Write(column);
                writer.Write(new string(' ', columnSizes[index] - column.Length + 1));
            }
            
            writer.WriteLine();
        }
    }

    private static void RenderList(StreamWriter writer, List<string> list)
    {
        foreach (var item in list)
        {
            writer.Write("  ");
            writer.WriteLine(item);
        }
    }

    private static void RenderScoring(StreamWriter writer, List<(LogLine Line, int Score)> scoredMessages)
    {
        writer.WriteLine($"--- Top Issues (showing max {scoredMessages.Count}) ---");
        writer.WriteLine();

        bool gotAtLeastOne = false;

        foreach (var (line, score) in scoredMessages)
        {
            gotAtLeastOne = true;
            
            writer.Write("  ");
            writer.Write(line.Source);
            writer.Write(" - ");
            writer.Write(line.LogLevel);
            writer.Write(" (");
            writer.Write(score);
            writer.Write(") Line #");
            writer.WriteLine(line.Line);
            writer.WriteLine(line.Contents.Trim().Replace("\n", "\n  "));
            writer.WriteLine();
        }
        
        if (!gotAtLeastOne)
            writer.WriteLine($"...Couldn't find anything important!");
        
        writer.WriteLine();
    }

    private static IEnumerable<KeyValuePair<TKey, TValue>> Without<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> enumerable,
        HashSet<TKey> keys
    )
    {
        foreach (var pair in enumerable)
        {
            if (!keys.Contains(pair.Key))
                yield return pair;
        }
    }
}