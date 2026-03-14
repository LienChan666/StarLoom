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
    public void ConfiguredWorkflow_Must_Still_Start_Artisan_List_After_PreStart_Work()
    {
        var orchestratorSource = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var dispatcherSource = ReadSource(Path.Combine("Automation", "WorkflowTaskDispatcher.cs"));

        Assert.Contains("CanStartArtisanList", orchestratorSource);
        Assert.Contains("P.Artisan.StartListById(C.ArtisanListId);", dispatcherSource);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Allow_Continuing_Managed_Artisan_List_On_Next_Cycle()
    {
        var orchestratorSource = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var validatorSource = ReadSource(Path.Combine("Automation", "WorkflowStartValidator.cs"));

        Assert.Contains("private bool artisanListManaged;", orchestratorSource);
        Assert.Contains("CanStartArtisanList(artisanListManaged, out error)", orchestratorSource);
        Assert.Contains("CanStartArtisanList(bool artisanListManaged, out string errorMessage)", validatorSource);
        Assert.Contains("if (artisanListManaged && P.Artisan.IsListRunning())", validatorSource);
    }

    [Fact]
    public void ArtisanIpc_Must_Continue_Running_List_Via_Ipc_Instead_Of_Chat_Restart()
    {
        var source = ReadSource(Path.Combine("GameInterop", "IPC", "ArtisanIpc.cs"));
        var startListSource = ReadMethodBody(source, "public void StartListById(int listId)")!;

        Assert.Contains("if (IsListRunning())", startListSource);
        Assert.Contains("SetStopRequest(false);", startListSource);
        Assert.Contains("SetListPause(false);", startListSource);
        Assert.Contains("Chat.SendMessage($\"/artisan lists {listId} start\");", startListSource);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Define_Closed_Loop_Stages_In_State_Model()
    {
        var stateSource = ReadSource(Path.Combine("Automation", "WorkflowState.cs"));

        Assert.Contains("MonitoringArtisan", stateSource);
        Assert.Contains("LoopingTurnInAndPurchase", stateSource);
        Assert.Contains("FinalizingCompletion", stateSource);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Not_End_Whole_Workflow_After_A_Single_Turn_When_Targets_Remain()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.DoesNotContain("State is WorkflowState.Running or WorkflowState.ReturningToCraftPoint", source);
        Assert.Contains("WorkflowState.LoopingTurnInAndPurchase", source);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Define_Exit_Branches_For_Final_Completion_And_Next_Craft_Cycle()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("WorkflowState.MonitoringArtisan", source);
        Assert.Contains("WorkflowState.FinalizingCompletion", source);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Return_To_Crafting_When_TurnIn_Items_Are_Exhausted_But_Targets_Remain()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var resumeSource = ReadMethodBody(source, "private void ResumeCraftingForNextCycle()")!;
        var loopHandlerSource = ReadMethodBody(source, "private void HandleLoopingTurnInAndPurchase()")!;

        Assert.Contains("ResumeCraftingForNextCycle();", loopHandlerSource);
        Assert.Contains("PrepareStartOrReturn(PendingStartKind.ConfiguredWorkflow)", resumeSource);
        Assert.DoesNotContain("dispatcher.DispatchConfiguredWorkflow();", resumeSource);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Recheck_And_Continue_Loop_After_Each_TurnIn_And_Purchase_Round()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var loopHandlerSource = ReadMethodBody(source, "private void HandleLoopingTurnInAndPurchase()")!;

        Assert.Contains("ShouldTakeOverForTurnInAndPurchase()", loopHandlerSource);
        Assert.Contains("EnterTurnInAndPurchaseLoop();", loopHandlerSource);
    }

    [Fact]
    public void WorkflowOrchestrator_Must_Not_Treat_Stopped_Artisan_As_Final_Completion_After_TurnIn_Loop()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var loopHandlerSource = ReadMethodBody(source, "private void HandleLoopingTurnInAndPurchase()")!;
        var loopFinalizeSource = ReadMethodBody(source, "private static bool ShouldFinalizeLoopAfterTurnInAndPurchase()")!;

        Assert.Contains("ShouldFinalizeLoopAfterTurnInAndPurchase()", loopHandlerSource);
        Assert.DoesNotContain("ShouldFinalizeConfiguredWorkflow()", loopHandlerSource);
        Assert.Contains("!HasPendingPurchaseWorkRemaining()", loopFinalizeSource);
        Assert.DoesNotContain("P.Artisan.IsListRunning()", loopFinalizeSource);
        Assert.Contains("ResumeCraftingForNextCycle();", loopHandlerSource);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Use_Free_Slot_Threshold_Before_Taking_Over_Artisan()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var thresholdSource = ReadMethodBody(source, "private static bool IsBelowFreeSlotThreshold()")!;
        var takeoverSource = ReadMethodBody(source, "private static bool ShouldTakeOverForTurnInAndPurchase()")!;

        Assert.Contains("C.FreeSlotThreshold > 0", thresholdSource);
        Assert.Contains("P.Inventory.GetFreeSlotCount() < C.FreeSlotThreshold", thresholdSource);
        Assert.Contains("P.Inventory.HasCollectableTurnIns()", takeoverSource);
        Assert.Contains("IsBelowFreeSlotThreshold()", takeoverSource);
        Assert.Contains("IsBelowFreeSlotThreshold()", source);
    }

    [Fact]
    public void Configuration_Must_Not_Expose_Legacy_BuyAfterEachTurnIn_Flag()
    {
        var source = ReadSource("Configuration.cs");

        Assert.DoesNotContain("BuyAfterEachTurnIn", source);
    }

    [Fact]
    public void PurchaseSettings_Must_Not_Render_Legacy_Auto_Buy_Checkbox()
    {
        var source = ReadSource(Path.Combine("UI", "Components", "Settings", "PurchaseSettingsCard.cs"));

        Assert.DoesNotContain("settings.purchase.auto_buy", source);
        Assert.DoesNotContain("BuyAfterEachTurnIn", source);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Default_To_Pending_Purchase_Work_Without_Legacy_Config_Switch()
    {
        var source = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var pendingSource = ReadMethodBody(source, "private static bool HasPendingPurchaseWorkRemaining()")!;
        var finalizeSource = ReadMethodBody(source, "private static bool ShouldFinalizeConfiguredWorkflow()")!;

        Assert.DoesNotContain("C.BuyAfterEachTurnIn", source);
        Assert.Contains("P.PurchaseResolver.HasPending()", pendingSource);
        Assert.Contains("HasPendingPurchaseWorkRemaining()", finalizeSource);
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

    private static string? ReadMethodBody(string source, string methodSignature)
    {
        var signatureIndex = source.IndexOf(methodSignature, StringComparison.Ordinal);
        if (signatureIndex < 0)
            return null;

        var bodyStart = source.IndexOf('{', signatureIndex);
        if (bodyStart < 0)
            return null;

        var depth = 0;
        for (var i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }
        }

        return null;
    }
}
