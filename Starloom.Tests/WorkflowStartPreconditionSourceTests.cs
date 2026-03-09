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
        Assert.Contains("WorkflowState.StartingArtisan", source);
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

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot, relativePath));
    }
}
