namespace BepinexLogAnalysis.Test;

public class BundledRulesTests
{
    [Fact]
    public async Task BundledList()
    {
        var list = BundledRules.Core;
        
        Assert.Null(list.GlobalSourceFilter);
        Assert.Single(list.Rules);
    }
}