using StarLoom.Tasks;
using Xunit;

namespace StarLoom.Tests.Tasks;

public sealed class WorkflowTaskTests
{
    [Fact]
    public void Update_Should_Switch_To_TurnIn_When_Artisan_Is_Paused_And_Bag_Is_Full()
    {
        var workflowTask = WorkflowTask.CreateForTests();
        workflowTask.StartConfiguredWorkflow();
        workflowTask.SetTestSnapshot(isBusy: true, shouldTakeOver: true, artisanPaused: true);

        workflowTask.Update();

        Assert.Equal("TurnIn", workflowTask.currentStage);
    }

    [Fact]
    public void Update_Should_Chain_Navigation_Before_Purchase_When_TurnIn_Completes()
    {
        var workflowTask = WorkflowTask.CreateForTests();
        workflowTask.StartConfiguredWorkflow();
        workflowTask.SetTestSnapshot(isBusy: true, shouldTakeOver: true, artisanPaused: true, useNavigation: false);
        workflowTask.Update();
        workflowTask.SetTestTurnInState(isCompleted: true);
        workflowTask.SetTestSnapshot(isBusy: true, shouldTakeOver: false, artisanPaused: true, hasConfiguredPurchases: true, useNavigation: true);

        workflowTask.Update();

        Assert.Equal("NavigatingToPurchase", workflowTask.currentStage);

        workflowTask.SetTestNavigationState(isCompleted: true);
        workflowTask.Update();

        Assert.Equal("Purchase", workflowTask.currentStage);
    }

    [Fact]
    public void Update_Should_Return_And_Resume_Artisan_After_Purchase_Cycle()
    {
        var workflowTask = WorkflowTask.CreateForTests();
        workflowTask.StartConfiguredWorkflow();
        workflowTask.SetTestSnapshot(isBusy: true, shouldTakeOver: true, artisanPaused: true, useNavigation: false);
        workflowTask.Update();
        workflowTask.SetTestTurnInState(isCompleted: true);
        workflowTask.SetTestSnapshot(isBusy: true, shouldTakeOver: false, artisanPaused: true, hasConfiguredPurchases: true, useNavigation: false);
        workflowTask.Update();
        workflowTask.SetTestPurchaseState(isCompleted: true);

        workflowTask.Update();

        Assert.Equal("Returning", workflowTask.currentStage);

        workflowTask.SetTestNavigationState(isCompleted: true);
        workflowTask.Update();

        Assert.Equal("MonitoringArtisan", workflowTask.currentStage);
        Assert.True(workflowTask.lastResumeRequested);
    }

    [Fact]
    public void StartConfiguredWorkflow_Should_Fail_With_User_Message_When_Artisan_Cannot_Be_Controlled()
    {
        var workflowTask = WorkflowTask.CreateForTests();
        workflowTask.SetTestControl(canControl: false, errorMessage: "Artisan IPC is unavailable.");

        workflowTask.StartConfiguredWorkflow();

        Assert.Equal("Failed", workflowTask.currentStage);
        Assert.Equal("Artisan IPC is unavailable.", workflowTask.lastErrorMessage);
    }
}
