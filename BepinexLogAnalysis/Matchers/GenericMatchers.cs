using BepinexLogAnalysis.Jobs;
using System.Text.RegularExpressions;

namespace BepinexLogAnalysis.SeverityRules;

public static partial class GenericMatchers
{
    public static readonly SeverityRule[] All = [
        new(BepinexMultipleLoads(), 6),
        new(BepinexMismatchedVersion(), -15),
        new(ExceptionThrown(), 15),
        new(NullReferenceExceptionThrown(), 4)
    ];

    [GeneratedRegex("""skipping.*version exists""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex BepinexMultipleLoads();

    [GeneratedRegex("""bepinex \(.+\) and might not work""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex BepinexMismatchedVersion();

    [GeneratedRegex("""[\s\w]*Exception""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex ExceptionThrown();

    [GeneratedRegex("""Object reference not set.*object""", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex NullReferenceExceptionThrown();
}
