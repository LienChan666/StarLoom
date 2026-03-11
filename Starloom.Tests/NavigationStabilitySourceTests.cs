using Xunit;

namespace Starloom.Tests;

public sealed class NavigationStabilitySourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void NavigationService_Still_Uses_Local_Action_Gate_And_Stability_Window()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "NavigationService.cs"));

        Assert.Contains("LocalPlayerActionGate", source);
        Assert.Contains("LocalActionReadyStableDuration", source);
        Assert.Contains("_localActionReadyAt", source);
        Assert.Contains("IsReadyForAutomation", source);
        Assert.DoesNotContain("StateMachine<NavigationStatus>", source);
        Assert.DoesNotContain("public NavigationStatus State", source);
        Assert.DoesNotContain("public void Update()", source);
    }

    [Fact]
    public void Navigation_Flow_Must_Be_Task_Driven_Instead_Of_Global_Update_Driven()
    {
        var orchestratorSource = File.ReadAllText(Path.Combine(RepoRoot, "Automation", "WorkflowOrchestrator.cs"));
        var actionsPath = Path.Combine(RepoRoot, "Tasks", "Actions", "NavigationActions.cs");

        Assert.DoesNotContain("P.Navigation.Update()", orchestratorSource);
        Assert.False(File.Exists(actionsPath), "未接管生产调用的 NavigationActions 应删除，避免双轨导航编排。");
    }
}
