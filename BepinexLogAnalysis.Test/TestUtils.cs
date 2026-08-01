using System.Reflection;

namespace BepinexLogAnalysis.Test;

public static class TestUtils
{
    public static Stream GetTestLog(string id)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        var stream = assembly.GetManifestResourceStream($"test_logs/{id}")
                           ?? throw new InvalidOperationException("Failed to get bundled rule list");
        return stream;
    }
}