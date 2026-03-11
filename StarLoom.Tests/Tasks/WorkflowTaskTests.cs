using StarLoom.Config;
using StarLoom.Tasks.Navigation;
using StarLoom.Tasks;
using Xunit;

namespace StarLoom.Tests.Tasks;

public sealed class WorkflowTaskTests
{
    [Fact]
    public void StartConfiguredWorkflow_Should_Fail_When_Collectable_Shop_Is_Missing()
    {
        var config = CreateConfig();
        config.preferredCollectableShop = null;
        var workflowTask = WorkflowTask.CreateForTests(config);
        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: false, isBusy: false, isPaused: false);

        workflowTask.StartConfiguredWorkflow();

        Assert.Equal("Failed", workflowTask.currentStage);
        Assert.Equal("A collectable shop must be configured before starting.", workflowTask.lastErrorMessage);
        Assert.Equal("state.orchestrator.running", workflowTask.GetStateKey());
    }

    [Fact]
    public void StartConfiguredWorkflow_Should_Return_To_Start_Location_Before_Monitoring()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig());
        workflowTask.SetTestLocation(isInsideHouse: false, isInsideInn: false);

        workflowTask.StartConfiguredWorkflow();

        Assert.Equal("Returning", workflowTask.currentStage);

        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestReturnState(isCompleted: true);
        workflowTask.Update();

        Assert.Equal("MonitoringArtisan", workflowTask.currentStage);
    }

    [Fact]
    public void Update_Should_Request_Pause_When_Free_Slots_Are_Below_Threshold_And_TurnIn_Work_Exists()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig(freeSlotThreshold: 5));
        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: false, isBusy: false, isPaused: false);
        workflowTask.StartConfiguredWorkflow();
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: true, isBusy: true, isPaused: false);
        workflowTask.SetTestInventoryState(freeSlotCount: 4, hasCollectableTurnIns: true, hasPendingPurchases: true);

        workflowTask.Update();

        Assert.Equal("WaitingForPause", workflowTask.currentStage);
        Assert.True(workflowTask.lastPauseRequested);
        Assert.Equal("state.orchestrator.waiting_pause", workflowTask.GetStateKey());
    }

    [Fact]
    public void Update_Should_Wait_For_Local_Control_Before_Starting_TurnIn()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig(freeSlotThreshold: 5));
        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: false, isBusy: false, isPaused: false);
        workflowTask.StartConfiguredWorkflow();

        workflowTask.SetTestInventoryState(freeSlotCount: 4, hasCollectableTurnIns: true, hasPendingPurchases: true);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: true, isBusy: true, isPaused: false);
        workflowTask.SetTestLocalPlayerReady(false);
        workflowTask.Update();
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: true, isBusy: true, isPaused: true);
        workflowTask.Update();

        Assert.Equal("WaitingForIdle", workflowTask.currentStage);
        Assert.Equal("state.orchestrator.waiting_idle", workflowTask.GetStateKey());

        workflowTask.SetTestLocalPlayerReady(true);
        workflowTask.Update();

        Assert.Equal("TurnIn", workflowTask.currentStage);
    }

    [Fact]
    public void Update_Should_Finalize_Configured_Workflow_When_Pending_Purchases_Are_Gone_After_TurnIn()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig(freeSlotThreshold: 5));
        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: false, isBusy: false, isPaused: false);
        workflowTask.StartConfiguredWorkflow();

        workflowTask.SetTestInventoryState(freeSlotCount: 4, hasCollectableTurnIns: true, hasPendingPurchases: true);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: true, isBusy: true, isPaused: true);
        workflowTask.SetTestLocalPlayerReady(true);
        workflowTask.Update();
        workflowTask.Update();
        workflowTask.Update();
        Assert.Equal("TurnIn", workflowTask.currentStage);

        workflowTask.SetTestInventoryState(freeSlotCount: 20, hasCollectableTurnIns: false, hasPendingPurchases: false);
        workflowTask.SetTestTurnInState(isCompleted: true);
        workflowTask.Update();

        Assert.Equal("Returning", workflowTask.currentStage);
        Assert.True(workflowTask.isBusy);
    }

    [Fact]
    public void Update_Should_Finalize_Configured_Workflow_When_Artisan_Stops_And_No_Takeover_Work_Remains()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig(freeSlotThreshold: 5));
        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: false, isBusy: false, isPaused: false);
        workflowTask.StartConfiguredWorkflow();

        workflowTask.SetTestInventoryState(freeSlotCount: 20, hasCollectableTurnIns: false, hasPendingPurchases: true);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: false, isBusy: false, isPaused: false);
        workflowTask.Update();

        Assert.Equal("Returning", workflowTask.currentStage);
        Assert.True(workflowTask.isBusy);
    }

    [Fact]
    public void StartTurnInOnly_Should_Fail_When_Collectable_Shop_Is_Missing()
    {
        var config = CreateConfig();
        config.preferredCollectableShop = null;
        var workflowTask = WorkflowTask.CreateForTests(config);

        workflowTask.StartTurnInOnly();

        Assert.Equal("Failed", workflowTask.currentStage);
        Assert.Equal("A collectable shop must be configured before starting.", workflowTask.lastErrorMessage);
    }

    [Fact]
    public void StartTurnInOnly_Should_Navigate_To_Collectable_Shop_When_Travel_Is_Required()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig());
        workflowTask.SetTestUseNavigation(true);

        workflowTask.StartTurnInOnly();

        Assert.Equal("NavigatingToTurnIn", workflowTask.currentStage);
    }

    [Fact]
    public void StartTurnInOnly_Should_Build_Collectable_Navigation_Request_From_Config()
    {
        var config = CreateConfig();
        var workflowTask = WorkflowTask.CreateForTests(config);
        workflowTask.SetTestUseNavigation(true);

        workflowTask.StartTurnInOnly();

        var request = Assert.IsType<NavigationRequest>(workflowTask.lastNavigationRequest);
        Assert.Equal(config.preferredCollectableShop!.location, request.destination);
        Assert.Equal(config.preferredCollectableShop.territoryId, request.territoryId);
        Assert.Equal(config.preferredCollectableShop.aetheryteId, request.aetheryteId);
        Assert.Equal(2f, request.arrivalDistance);
        Assert.True(request.useLifestream);
        Assert.Equal(config.preferredCollectableShop.lifestreamCommand, request.lifestreamCommand);
    }

    [Fact]
    public void StartPurchaseOnly_Should_Fail_When_Targets_Are_Already_Satisfied()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig());
        workflowTask.SetTestInventoryState(freeSlotCount: 20, hasCollectableTurnIns: false, hasPendingPurchases: false);

        workflowTask.StartPurchaseOnly();

        Assert.Equal("Failed", workflowTask.currentStage);
        Assert.Equal("All configured purchase items already reached their target quantities.", workflowTask.lastErrorMessage);
    }

    [Fact]
    public void StartPurchaseOnly_Should_Navigate_To_Scrip_Shop_When_Travel_Is_Required()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig());
        workflowTask.SetTestUseNavigation(true);

        workflowTask.StartPurchaseOnly();

        Assert.Equal("NavigatingToPurchase", workflowTask.currentStage);
    }

    [Fact]
    public void StartPurchaseOnly_Should_Build_Scrip_Shop_Navigation_Request_From_Config()
    {
        var config = CreateConfig();
        var workflowTask = WorkflowTask.CreateForTests(config);
        workflowTask.SetTestUseNavigation(true);

        workflowTask.StartPurchaseOnly();

        var request = Assert.IsType<NavigationRequest>(workflowTask.lastNavigationRequest);
        Assert.Equal(config.preferredCollectableShop!.scripShopLocation, request.destination);
        Assert.Equal(config.preferredCollectableShop.territoryId, request.territoryId);
        Assert.Equal(config.preferredCollectableShop.aetheryteId, request.aetheryteId);
        Assert.Equal(0.4f, request.arrivalDistance);
        Assert.True(request.useLifestream);
        Assert.Equal(config.preferredCollectableShop.lifestreamCommand, request.lifestreamCommand);
    }

    [Fact]
    public void StartConfiguredWorkflow_Should_Fail_With_User_Message_When_Artisan_Cannot_Be_Controlled()
    {
        var workflowTask = WorkflowTask.CreateForTests(CreateConfig());
        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestControl(canControl: false, errorMessage: "Artisan is busy with another task.");

        workflowTask.StartConfiguredWorkflow();

        Assert.Equal("Failed", workflowTask.currentStage);
        Assert.Equal("Artisan is busy with another task.", workflowTask.lastErrorMessage);
    }

    [Fact]
    public void Update_Should_Route_To_Return_After_Purchase_When_Post_Action_Is_ReturnToConfiguredPoint()
    {
        var config = CreateConfig();
        config.postPurchaseAction = PostPurchaseAction.ReturnToConfiguredPoint;
        var workflowTask = WorkflowTask.CreateForTests(config);

        DriveConfiguredWorkflowToPurchase(workflowTask);

        workflowTask.SetTestPurchaseState(isCompleted: true);
        workflowTask.Update();

        Assert.Equal("Returning", workflowTask.currentStage);
    }

    [Fact]
    public void Update_Should_Route_To_CloseGame_After_Purchase_When_Post_Action_Is_CloseGame()
    {
        var config = CreateConfig();
        config.postPurchaseAction = PostPurchaseAction.CloseGame;
        var workflowTask = WorkflowTask.CreateForTests(config);

        DriveConfiguredWorkflowToPurchase(workflowTask);

        workflowTask.SetTestPurchaseState(isCompleted: true);
        workflowTask.Update();

        Assert.Equal("ClosingGame", workflowTask.currentStage);
    }

    private static void DriveConfiguredWorkflowToPurchase(WorkflowTask workflowTask)
    {
        workflowTask.SetTestLocation(isInsideHouse: true, isInsideInn: false);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: false, isBusy: false, isPaused: false);
        workflowTask.SetTestUseNavigation(false);

        workflowTask.StartConfiguredWorkflow();

        workflowTask.SetTestInventoryState(freeSlotCount: 4, hasCollectableTurnIns: true, hasPendingPurchases: true);
        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: true, isBusy: true, isPaused: false);
        workflowTask.SetTestLocalPlayerReady(true);
        workflowTask.Update();

        workflowTask.SetTestArtisanState(isAvailable: true, isListRunning: true, isBusy: true, isPaused: true);
        workflowTask.Update();
        workflowTask.Update();

        Assert.Equal("TurnIn", workflowTask.currentStage);

        workflowTask.SetTestTurnInState(isCompleted: true);
        workflowTask.Update();

        Assert.Equal("Purchase", workflowTask.currentStage);
    }

    private static PluginConfig CreateConfig(int freeSlotThreshold = 10)
    {
        return new PluginConfig
        {
            artisanListId = 7,
            defaultReturnPoint = ReturnPointConfig.CreateInn(),
            preferredCollectableShop = new CollectableShopConfig
            {
                location = new System.Numerics.Vector3(10f, 0f, 20f),
                territoryId = 1,
                npcId = 101,
                scripShopNpcId = 202,
                scripShopLocation = new System.Numerics.Vector3(30f, 0f, 40f),
                aetheryteId = 99,
                isLifestreamRequired = true,
                lifestreamCommand = "/li house",
                displayName = "Collectable Shop",
            },
            freeSlotThreshold = freeSlotThreshold,
            scripShopItems =
            [
                new PurchaseItemConfig
                {
                    itemId = 100,
                    itemName = "Token",
                    targetCount = 3,
                    scripCost = 1,
                    page = "Materials",
                    subPage = "Scrip Exchange",
                },
            ],
        };
    }
}
