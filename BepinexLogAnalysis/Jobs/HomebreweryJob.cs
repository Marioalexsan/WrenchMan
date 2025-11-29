using BepinexLogAnalysis.SeverityRules;
using System.Data;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.Jobs;

public partial class HomebreweryJob(TopIssuesJob topIssuesJob) : IJob
{
    private readonly TopIssuesJob _topIssuesJob = topIssuesJob;

    // Type => Thing Name => Asset Name
    private readonly Dictionary<string, Dictionary<string, string>> _brokenStuff = [];
    private readonly List<string> _multipleMeshes = [];

    public void ProcessLog(LogLine line, Dictionary<string, string> context)
    {
        if (line.Source != KnownSources.Homebrewery)
            return;

        if (!context.TryGetValue("game", out var game) && game != KnownGames.Atlyss)
            return;

        Match invalidMatch = AtlyssMatchers.HomebreweryThingInvalid().Match(line.Contents);

        if (invalidMatch.Success)
        {
            var objName = invalidMatch.Groups[1].Value;
            var objType = MapAssetName(invalidMatch.Groups[2].Value);
            var assetName = invalidMatch.Groups[3].Value;

            if (!_brokenStuff.TryGetValue(objType, out var assetStuff))
                assetStuff = _brokenStuff[objType] = [];

            assetStuff[objName] = assetName;
            _topIssuesJob.RemoveLineFromScoring(line);
            return;
        }

        Match multipleMeshes = AtlyssMatchers.HomebreweryMultipleMeshes().Match(line.Contents);

        if (multipleMeshes.Success)
        {
            _multipleMeshes.Add(multipleMeshes.Groups[1].Value);
            _topIssuesJob.RemoveLineFromScoring(line);
            return;
        }
    }

    public void OutputResults(StreamWriter stream)
    {
        if (_brokenStuff.Count == 0)
            return;

        stream.WriteLine("--- Homebrewery Issues ---");
        stream.WriteLine();

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

    public void Reset()
    {
        _brokenStuff.Clear();
        _multipleMeshes.Clear();
    }

    public void OnLogBegin()
    {
        // Nothing
    }

    public void OnLogEnd()
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
        "_weaponProjectileSet" => "",
        "cond1name" => "condition name",
        "cond2name" => "condition name",
        "cond3name" => "condition name",
        "cond4name" => "condition name",
        "cond5name" => "condition name",
        _ => input
    };
}
