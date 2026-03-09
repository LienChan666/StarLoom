using Xunit;

namespace Starloom.Tests;

public sealed class WorkflowOrchestratorSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void MainWorkflow_Must_Define_WorkflowOrchestrator_Source_File()
    {
        var orchestratorPath = Path.Combine(RepoRoot, "Automation", "WorkflowOrchestrator.cs");

        Assert.True(File.Exists(orchestratorPath), "缺少新的 WorkflowOrchestrator 总控文件。");
    }

    [Fact]
    public void Starloom_EntryPoint_Must_Not_Keep_ArtisanSession_Field()
    {
        var source = ReadSource("Starloom.cs");

        Assert.DoesNotContain("internal ArtisanSession Session;", source);
    }

    [Fact]
    public void Starloom_EntryPoint_Must_Expose_WorkflowOrchestrator_Field()
    {
        var source = ReadSource("Starloom.cs");

        Assert.Contains("internal WorkflowOrchestrator Automation;", source);
        Assert.DoesNotContain("internal AutomationController Automation;", source);
    }

    [Fact]
    public void Starloom_OnUpdate_Must_Advance_Only_WorkflowOrchestrator()
    {
        var source = ReadSource("Starloom.cs");

        Assert.Contains("Automation.Update();", source);
    }

    [Fact]
    public void Legacy_Workflows_Must_Not_Remain_Main_Entry_Point()
    {
        var controllerPath = Path.Combine(RepoRoot, "Automation", "AutomationController.cs");
        if (!File.Exists(controllerPath))
            return;

        var source = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("using Starloom.Tasks;", source);
        Assert.DoesNotContain("Workflows.", source);
    }

    [Fact]
    public void WorkflowOrchestrator_Must_Use_Dispatcher_Instead_Of_Legacy_Workflows()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.DoesNotContain("Workflows.", source);
        Assert.Contains("dispatcher.DispatchCollectableTurnIn()", source);
        Assert.Contains("dispatcher.DispatchPurchaseOnly()", source);
    }

    [Fact]
    public void Legacy_Workflows_File_Must_No_Longer_Own_High_Level_Composition()
    {
        var path = Path.Combine(RepoRoot, "Tasks", "Workflows.cs");
        if (!File.Exists(path))
            return;

        var source = File.ReadAllText(path);

        Assert.DoesNotContain("TaskArtisanPause.EnqueueIfNeeded();", source);
        Assert.DoesNotContain("TaskCollectableTurnIn.Enqueue();", source);
        Assert.DoesNotContain("TaskScripPurchase.Enqueue();", source);
    }

    [Fact]
    public void Legacy_Session_And_Workflow_Files_Must_Be_Removed()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot, "Automation", "ArtisanSession.cs")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, "Tasks", "Workflows.cs")));
    }

    [Fact]
    public void WorkflowOrchestrator_Must_Centralize_Stop_And_Failure_Cleanup()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("P.TM.Abort();", source);
        Assert.Contains("P.Navigation.Stop();", source);
        Assert.Contains("P.CollectableTurnIn.Stop();", source);
        Assert.Contains("P.ScripPurchase.Stop();", source);
        Assert.Contains("P.Artisan.SetStopRequest(true);", source);
        Assert.Contains("State = WorkflowState.Failed;", source);
        Assert.Contains("State = WorkflowState.Idle;", source);
    }

    [Fact]
    public void CollectableTurnIn_Service_And_Task_Must_No_Longer_Expose_Long_Lived_State_Machine()
    {
        var serviceSource = ReadSource(Path.Combine("Services", "CollectableTurnInService.cs"));
        var taskSource = ReadSource(Path.Combine("Tasks", "TaskCollectableTurnIn.cs"));

        Assert.DoesNotContain("StateMachine<CollectableTurnInState>", serviceSource);
        Assert.DoesNotContain("public CollectableTurnInState State", serviceSource);
        Assert.DoesNotContain("WaitForCompletion", taskSource);
        Assert.DoesNotContain("P.CollectableTurnIn.State", taskSource);
    }

    [Fact]
    public void ScripPurchase_Service_And_Task_Must_No_Longer_Expose_Long_Lived_State_Machine()
    {
        var serviceSource = ReadSource(Path.Combine("Services", "ScripPurchaseService.cs"));
        var taskSource = ReadSource(Path.Combine("Tasks", "TaskScripPurchase.cs"));

        Assert.DoesNotContain("StateMachine<ScripPurchasePhase>", serviceSource);
        Assert.DoesNotContain("public ScripPurchasePhase State", serviceSource);
        Assert.DoesNotContain("WaitForCompletion", taskSource);
        Assert.DoesNotContain("P.ScripPurchase.State", taskSource);
    }

    [Fact]
    public void AutomationController_Must_Not_Carry_Global_Orchestration_Responsibilities()
    {
        var controllerPath = Path.Combine(RepoRoot, "Automation", "AutomationController.cs");
        if (!File.Exists(controllerPath))
            return;

        var source = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("P.Session", source);
        Assert.DoesNotContain("StartConfiguredWorkflow", source);
        Assert.DoesNotContain("StartCollectableTurnIn", source);
        Assert.DoesNotContain("StartPurchaseOnly", source);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot, relativePath));
    }
}
