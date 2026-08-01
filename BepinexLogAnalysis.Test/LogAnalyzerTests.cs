namespace BepinexLogAnalysis.Test;

public class LogAnalyzerTests
{
    [Fact]
    public async Task Test1()
    {
        var logAnalyzer = new LogAnalyzer(new LogAnalyzerOptions()
        {
            RuleLists = [
                BundledRules.Core,
                BundledRules.BasicScoring,
                .. BundledRules.Atlyss,
            ]
        });

        using var input = TestUtils.GetTestLog("Log-2");
        var result = await logAnalyzer.ProcessLogAsync(input);
        Assert.NotEmpty(result.ScoredMessages);
        Assert.NotEmpty(result.Content.Keys);

        var renderedResult = new MemoryStream();
        
        Renderer.WrenchManRender(result, renderedResult);
        renderedResult.Position = 0;

        using var reader = new StreamReader(renderedResult);

        var content = reader.ReadToEnd();
        Assert.NotEmpty(content);
    }
}