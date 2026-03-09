using Xunit;

namespace Starloom.Tests;

public sealed class CollectablePurchaseFlowSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void WorkflowOrchestrator_Treats_House_And_Inn_As_Valid_Internal_Start_Locations()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("HousingReturnPointService.IsInsideHouse() || HousingReturnPointService.IsInsideInn()", source);
    }
}
