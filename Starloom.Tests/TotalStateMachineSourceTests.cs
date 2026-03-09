using Xunit;

namespace Starloom.Tests;

public sealed class TotalStateMachineSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void HomeControlPane_Must_Not_Read_Session_State_Directly()
    {
        var source = ReadSource(Path.Combine("UI", "Components", "Home", "HomeControlPane.cs"));

        Assert.DoesNotContain("P.Session.State", source);
        Assert.DoesNotContain("P.Session.GetStateKey()", source);
    }

    [Fact]
    public void HomeControlPane_Must_Read_Only_Unified_WorkflowOrchestrator_State_Key()
    {
        var source = ReadSource(Path.Combine("UI", "Components", "Home", "HomeControlPane.cs"));

        Assert.Contains("P.Automation.GetStateKey()", source);
        Assert.DoesNotContain("state.orchestrator.running", source);
        Assert.DoesNotContain("state.orchestrator.idle", source);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot, relativePath));
    }
}
