using Xunit;

namespace Starloom.Tests;

public sealed class WorkflowStartPreconditionSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void WorkflowOrchestrator_Must_Validate_Start_Location_Before_Starting_Workflow()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("HousingReturnPointService.IsInsideHouse()", source);
        Assert.Contains("HousingReturnPointService.IsInsideInn()", source);
        Assert.Contains("DispatchStartReturn", source);
    }

    [Fact]
    public void WorkflowOrchestrator_Must_Not_Start_Artisan_Before_Location_Precondition_Passes()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("if (!IsInsideStartLocation())", source);
        Assert.Contains("dispatcher.DispatchConfiguredWorkflow(artisanListManaged);", source);
        Assert.Contains("State = WorkflowState.MonitoringArtisan;", source);
        Assert.Contains("ResumePendingStartAfterReturn", source);
    }

    [Fact]
    public void WorkflowOrchestrator_Must_Record_Pending_Start_And_Resume_It_After_Return_Completes()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("pendingStart", source);
        Assert.Contains("WorkflowState.WaitingForStartReturn", source);
        Assert.Contains("if (!P.TM.IsBusy && State == WorkflowState.WaitingForStartReturn)", source);
    }

    [Fact]
    public void StartCollectableTurnIn_Must_Not_Depend_On_Returning_Inside_Before_Start()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("internal void StartCollectableTurnIn()", source);
        Assert.DoesNotContain("PrepareStartOrReturn(PendingStartKind.CollectableTurnIn)", source);
        Assert.Contains("StartCollectableTurnInCore();", source);
    }

    [Fact]
    public void StartPurchaseOnly_Must_Not_Depend_On_Returning_Inside_Before_Start()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("internal void StartPurchaseOnly()", source);
        Assert.DoesNotContain("PrepareStartOrReturn(PendingStartKind.PurchaseOnly)", source);
        Assert.Contains("StartPurchaseOnlyCore();", source);
    }

    [Fact]
    public void StartLocation_Return_Precondition_Must_Be_Reserved_For_Configured_Workflow_Only()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("PrepareStartOrReturn(PendingStartKind.ConfiguredWorkflow)", source);
        Assert.DoesNotContain("PrepareStartOrReturn(PendingStartKind.CollectableTurnIn)", source);
        Assert.DoesNotContain("PrepareStartOrReturn(PendingStartKind.PurchaseOnly)", source);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot, relativePath));
    }
}
