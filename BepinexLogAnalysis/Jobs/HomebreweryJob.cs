using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public partial class HomebreweryJob : IJob
{
    [GeneratedRegex("""(.*) : (.*) => JSON parse error: (.*)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex JsonParseError();

    [GeneratedRegex("""(.*?) (\S*) (?:name )?invalid: ([\S\ ]*)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex ThingInvalid();

    [GeneratedRegex("""(.*) - Glb file returned more than one mesh, we only want one!""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex MultipleMeshes();

    [GeneratedRegex("""Processing: (\S*)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex LoadingContentPack();

    // Type => Thing Name => Asset Name
    private readonly Dictionary<string, Dictionary<string, string>> _brokenStuff = [];
    private readonly List<string> _multipleMeshes = [];
    private bool _encounteredLogLines;
    private bool _encounteredIssues;
    private bool _foundContentPacks;

    public bool ExtractedAnyData => _encounteredIssues || _foundContentPacks;

    public bool ProcessLog(LogContext context, LogLine line)
    {
        if (line.Source != KnownSources.Homebrewery)
            return true;

        if (context.Game != KnownGames.Atlyss)
            return true;

        _encounteredLogLines = true;

        Match invalidMatch = ThingInvalid().Match(line.Contents);

        if (invalidMatch.Success)
        {
            var objName = invalidMatch.Groups[1].Value;
            var objType = MapAssetName(invalidMatch.Groups[2].Value);
            var assetName = invalidMatch.Groups[3].Value;

            if (!_brokenStuff.TryGetValue(objType, out var assetStuff))
                assetStuff = _brokenStuff[objType] = [];

            assetStuff[objName] = assetName;
            _encounteredIssues = true;
            return false;
        }

        Match multipleMeshes = MultipleMeshes().Match(line.Contents);

        if (multipleMeshes.Success)
        {
            _multipleMeshes.Add(multipleMeshes.Groups[1].Value);
            _encounteredIssues = true;
            return false;
        }

        Match jsonParseError = JsonParseError().Match(line.Contents);

        if (jsonParseError.Success)
        {
            var objName = jsonParseError.Groups[2].Value;
            var error = jsonParseError.Groups[3].Value;

            if (!_brokenStuff.TryGetValue("JSON", out var assetStuff))
                assetStuff = _brokenStuff["JSON"] = [];

            assetStuff[objName] = error;
            _encounteredIssues = true;
            return false;
        }

        Match loadingContentPack = LoadingContentPack().Match(line.Contents);

        if (loadingContentPack.Success)
        {
            var contentPackName = loadingContentPack.Groups[1].Value;
            
            context.AddPlugin("Homebrewery content packs", contentPackName, null);
            _foundContentPacks = true;
            return false;
        }

        return true;
    }

    public void OutputResults(LogContext context, StreamWriter stream)
    {
        if (!_encounteredLogLines)
            return;

        stream.WriteLine("--- Homebrewery Issues ---");
        stream.WriteLine();

        if (!_encounteredIssues)
        {
            stream.WriteLine("...no issues found!");
            stream.WriteLine();
            return;
        }

        foreach (var objType in _brokenStuff.OrderBy(x => x.Key))
        {
            stream.Write("Invalid ");
            stream.Write(MapAssetName(objType.Key));
            stream.WriteLine(':');

            foreach (var objName in objType.Value.OrderBy(x => x.Key))
            {
                stream.Write("  ");
                stream.Write(objName.Key);
                stream.Write(" - ");
                stream.WriteLine(objName.Value);
            }

            stream.WriteLine();
        }

        if (_multipleMeshes.Count > 0)
        {
            _multipleMeshes.Sort();

            stream.WriteLine("Multiple meshes in GLB (expected only one):");

            foreach (var objName in _multipleMeshes)
            {
                stream.Write("  ");
                stream.WriteLine(objName);
            }
        }

        stream.WriteLine();
    }

    public void Reset(LogContext context)
    {
        _brokenStuff.Clear();
        _multipleMeshes.Clear();
        _encounteredLogLines = false;
        _encounteredIssues = false;
        _foundContentPacks = false;
    }

    public void OnLogBegin(LogContext context)
    {
        // Nothing
    }

    public void OnLogEnd(LogContext context)
    {
        // Nothing
    }

    private static string MapAssetName(string input) => input switch
    {
        "_capeMesh" => "cape mesh",
        "_neckCollarMesh" => "neck collar mesh",
        "_chestRenderDisplay" => "chest render display",
        "_robeSkirtRender" => "robe skirt render",
        "_armCuffRender" => "arm cuff render",
        "_shoulderpadMesh" => "shoulderpad mesh",
        "_hipMesh" => "hip mesh",
        "_helmRender" => "helm render",
        "_helmOverrideMesh" => "helm override mesh",
        "_legPieceRender_01" => "leg piece render",
        "_legPieceRender_02" => "leg piece render",
        "_legPieceRender_03" => "leg piece render",
        "_legPieceRender_04" => "leg piece render",
        "_shieldMesh" => "shield mesh",
        "weaponMesh" => "weapon mesh",
        "weaponType" => "weapon type",
        "_drawSound" => "draw sound",
        "_swingSound" => "swing sound",
        "_hitSound" => "hit sound",
        "_weaponProjectileSet" => "weapon projectile set",
        "cond1name" => "condition name",
        "cond2name" => "condition name",
        "cond3name" => "condition name",
        "cond4name" => "condition name",
        "cond5name" => "condition name",
        _ => input
    };
}
