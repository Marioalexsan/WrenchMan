using BepinexLogAnalysis.Jobs;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.SeverityRules;

public static partial class AtlyssMatchers
{
    public static readonly SeverityRule[] All = [
        new(UiTweaksExpBarError(), -15),
        new(BepinexFixer(), -15),
        new(EasySettingsVersionWarnFromMarioalexsan(), -9999), // Don't care
    ];

    [GeneratedRegex("""ATLYSS_UiTweaks\.Harmony_Patches\.DisplayXPToNextLevelPatch\.patchExpBar""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex UiTweaksExpBarError();

    [GeneratedRegex("""GameManager_Init_CacheExplorer""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex BepinexFixer();

    [GeneratedRegex("""Waking up at (\S*) (\S*) (AM|PM) UTC\.\.\. Game Version is: ([\S\ ]*)""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex HomebreweryWakeup();

    [GeneratedRegex("""Soft dependency EasySettings has a different version than expected""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex EasySettingsVersionWarnFromMarioalexsan();
}
