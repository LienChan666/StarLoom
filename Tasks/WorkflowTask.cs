using System.Numerics;
using StarLoom.Config;
using StarLoom.Ipc;
using StarLoom.Tasks.Artisan;
using StarLoom.Tasks.Navigation;
using StarLoom.Tasks.Purchase;
using StarLoom.Tasks.TurnIn;

namespace StarLoom.Tasks;

public sealed class WorkflowTask
{
    private enum WorkflowMode
    {
        None,
        ConfiguredWorkflow,
        TurnInOnly,
        PurchaseOnly,
    }

    private enum WorkflowStage
    {
        Idle,
        MonitoringArtisan,
        NavigatingToTurnIn,
        TurnIn,
        NavigatingToPurchase,
        Purchase,
        Returning,
        Failed,
    }

    private readonly PluginConfig pluginConfig;
    private readonly ArtisanTask artisanTask;
    private readonly NavigationTask navigationTask;
    private readonly TurnInTask turnInTask;
    private readonly PurchaseTask purchaseTask;

    private WorkflowMode workflowMode;
    private WorkflowStage workflowStage;
    private bool isTestMode;
    private bool testCanControl = true;
    private bool testIsBusy;
    private bool testShouldTakeOver;
    private bool testArtisanPaused;
    private bool testHasConfiguredPurchases = true;
    private bool testUseNavigation;
    private bool testNavigationCompleted;
    private bool testNavigationFailed;
    private bool testTurnInCompleted;
    private bool testTurnInFailed;
    private bool testPurchaseCompleted;
    private bool testPurchaseFailed;
    private string? testControlErrorMessage;

    public string currentStage => workflowStage switch
    {
        WorkflowStage.Idle => "Idle",
        WorkflowStage.MonitoringArtisan => "MonitoringArtisan",
        WorkflowStage.NavigatingToTurnIn => "NavigatingToTurnIn",
        WorkflowStage.TurnIn => "TurnIn",
        WorkflowStage.NavigatingToPurchase => "NavigatingToPurchase",
        WorkflowStage.Purchase => "Purchase",
        WorkflowStage.Returning => "Returning",
        WorkflowStage.Failed => "Failed",
        _ => "Idle",
    };

    public bool isBusy => workflowStage is not WorkflowStage.Idle;
    public bool lastResumeRequested { get; private set; }
    public string? lastErrorMessage { get; private set; }

    public WorkflowTask() : this(
        new PluginConfig(),
        new ArtisanTask(new ArtisanIpc(), new PluginConfig()),
        new NavigationTask(new VNavmeshIpc(), new LifestreamIpc()),
        new TurnInTask(),
        new PurchaseTask())
    {
    }

    public WorkflowTask(
        PluginConfig pluginConfig,
        ArtisanTask artisanTask,
        NavigationTask navigationTask,
        TurnInTask turnInTask,
        PurchaseTask purchaseTask)
    {
        this.pluginConfig = pluginConfig;
        this.artisanTask = artisanTask;
        this.navigationTask = navigationTask;
        this.turnInTask = turnInTask;
        this.purchaseTask = purchaseTask;
    }

    public static WorkflowTask CreateForTests()
    {
        var pluginConfig = new PluginConfig
        {
            artisanListId = 7,
            preferredCollectableShop = new CollectableShopConfig
            {
                territoryId = 1,
                displayName = "Collectable Shop",
            },
            defaultReturnPoint = ReturnPointConfig.CreateInn(),
        };

        var workflowTask = new WorkflowTask(
            pluginConfig,
            new ArtisanTask(new NoOpArtisanIpc(), pluginConfig),
            new NavigationTask(new NoOpVNavmeshIpc(), new NoOpLifestreamIpc()),
            new TurnInTask(),
            new PurchaseTask(pluginConfig, new Game.InventoryGame(), new Game.ScripShopGame()));

        workflowTask.isTestMode = true;
        return workflowTask;
    }

    public void StartConfiguredWorkflow()
    {
        if (isBusy)
            return;

        workflowMode = WorkflowMode.ConfiguredWorkflow;
        lastResumeRequested = false;
        lastErrorMessage = null;

        if (!CanControlArtisan(out var errorMessage))
        {
            Fail(errorMessage);
            return;
        }

        if (!isTestMode && !artisanTask.StartConfiguredList())
        {
            Fail("Failed to start configured Artisan list.");
            return;
        }

        workflowStage = WorkflowStage.MonitoringArtisan;
    }

    public void StartTurnInOnly()
    {
        if (isBusy)
            return;

        workflowMode = WorkflowMode.TurnInOnly;
        lastResumeRequested = false;
        lastErrorMessage = null;
        BeginTurnIn();
    }

    public void StartPurchaseOnly()
    {
        if (isBusy)
            return;

        workflowMode = WorkflowMode.PurchaseOnly;
        lastResumeRequested = false;
        lastErrorMessage = null;
        BeginPurchase();
    }

    public void Stop()
    {
        artisanTask.Stop();
        navigationTask.Stop();
        turnInTask.Stop();
        purchaseTask.Stop();
        workflowMode = WorkflowMode.None;
        workflowStage = WorkflowStage.Idle;
        lastResumeRequested = false;
        lastErrorMessage = null;
    }

    public void Update()
    {
        switch (workflowStage)
        {
            case WorkflowStage.MonitoringArtisan:
                HandleMonitoringArtisan();
                return;
            case WorkflowStage.NavigatingToTurnIn:
                HandleNavigatingToTurnIn();
                return;
            case WorkflowStage.TurnIn:
                HandleTurnIn();
                return;
            case WorkflowStage.NavigatingToPurchase:
                HandleNavigatingToPurchase();
                return;
            case WorkflowStage.Purchase:
                HandlePurchase();
                return;
            case WorkflowStage.Returning:
                HandleReturning();
                return;
            default:
                return;
        }
    }

    public string GetStateText()
    {
        return currentStage;
    }

    public string GetStateKey()
    {
        return workflowStage switch
        {
            WorkflowStage.Idle => "state.orchestrator.idle",
            WorkflowStage.MonitoringArtisan => "state.session.monitoring",
            WorkflowStage.NavigatingToTurnIn => "state.orchestrator.running",
            WorkflowStage.TurnIn => "state.orchestrator.running",
            WorkflowStage.NavigatingToPurchase => "state.orchestrator.running",
            WorkflowStage.Purchase => "state.orchestrator.running",
            WorkflowStage.Returning => "state.orchestrator.running",
            WorkflowStage.Failed => "state.orchestrator.failed",
            _ => "state.orchestrator.idle",
        };
    }

    public void SetTestSnapshot(
        bool isBusy,
        bool shouldTakeOver,
        bool artisanPaused,
        bool hasConfiguredPurchases = true,
        bool useNavigation = false)
    {
        isTestMode = true;
        testIsBusy = isBusy;
        testShouldTakeOver = shouldTakeOver;
        testArtisanPaused = artisanPaused;
        testHasConfiguredPurchases = hasConfiguredPurchases;
        testUseNavigation = useNavigation;
    }

    public void SetTestTurnInState(bool isCompleted, bool hasFailed = false)
    {
        isTestMode = true;
        testTurnInCompleted = isCompleted;
        testTurnInFailed = hasFailed;
    }

    public void SetTestPurchaseState(bool isCompleted, bool hasFailed = false)
    {
        isTestMode = true;
        testPurchaseCompleted = isCompleted;
        testPurchaseFailed = hasFailed;
    }

    public void SetTestNavigationState(bool isCompleted, bool hasFailed = false)
    {
        isTestMode = true;
        testNavigationCompleted = isCompleted;
        testNavigationFailed = hasFailed;
    }

    public void SetTestControl(bool canControl, string errorMessage)
    {
        isTestMode = true;
        testCanControl = canControl;
        testControlErrorMessage = errorMessage;
    }

    private void HandleMonitoringArtisan()
    {
        if (!ShouldTakeOver())
            return;

        if (!IsArtisanPaused())
        {
            if (!isTestMode)
                artisanTask.Pause();

            return;
        }

        if (ShouldUseNavigation())
        {
            BeginNavigationToTurnIn();
            return;
        }

        BeginTurnIn();
    }

    private void HandleNavigatingToTurnIn()
    {
        if (HasNavigationFailed())
        {
            Fail(GetNavigationErrorMessage());
            ConsumeNavigationState();
            return;
        }

        if (!HasNavigationCompleted())
            return;

        ConsumeNavigationState();
        BeginTurnIn();
    }

    private void HandleTurnIn()
    {
        if (HasTurnInFailed())
        {
            Fail("Turn-in task failed.");
            ConsumeTurnInState();
            return;
        }

        if (!HasTurnInCompleted())
            return;

        ConsumeTurnInState();

        if (HasConfiguredPurchases())
        {
            if (ShouldUseNavigation())
            {
                BeginNavigationToPurchase();
                return;
            }

            BeginPurchase();
            return;
        }

        BeginReturn();
    }

    private void HandleNavigatingToPurchase()
    {
        if (HasNavigationFailed())
        {
            Fail(GetNavigationErrorMessage());
            ConsumeNavigationState();
            return;
        }

        if (!HasNavigationCompleted())
            return;

        ConsumeNavigationState();
        BeginPurchase();
    }

    private void HandlePurchase()
    {
        if (HasPurchaseFailed())
        {
            Fail("Purchase task failed.");
            ConsumePurchaseState();
            return;
        }

        if (!HasPurchaseCompleted())
            return;

        ConsumePurchaseState();
        BeginReturn();
    }

    private void HandleReturning()
    {
        if (HasNavigationFailed())
        {
            Fail(GetNavigationErrorMessage());
            ConsumeNavigationState();
            return;
        }

        if (!HasNavigationCompleted())
            return;

        ConsumeNavigationState();

        if (workflowMode == WorkflowMode.ConfiguredWorkflow)
        {
            RequestArtisanResume();
            workflowStage = WorkflowStage.MonitoringArtisan;
            return;
        }

        workflowMode = WorkflowMode.None;
        workflowStage = WorkflowStage.Idle;
    }

    private void BeginNavigationToTurnIn()
    {
        if (!isTestMode)
            navigationTask.Start(BuildNavigationRequest("collectable", pluginConfig.preferredCollectableShop?.territoryId ?? 0));

        workflowStage = WorkflowStage.NavigatingToTurnIn;
    }

    private void BeginTurnIn()
    {
        if (!isTestMode)
            turnInTask.Start();

        workflowStage = WorkflowStage.TurnIn;
    }

    private void BeginNavigationToPurchase()
    {
        if (!isTestMode)
            navigationTask.Start(BuildNavigationRequest("purchase", pluginConfig.preferredCollectableShop?.territoryId ?? 0));

        workflowStage = WorkflowStage.NavigatingToPurchase;
    }

    private void BeginPurchase()
    {
        if (!isTestMode)
            purchaseTask.Start(currentScrips: 0);

        workflowStage = WorkflowStage.Purchase;
    }

    private void BeginReturn()
    {
        if (!isTestMode)
            navigationTask.Start(BuildNavigationRequest("return", pluginConfig.defaultReturnPoint?.territoryId ?? 0));

        workflowStage = WorkflowStage.Returning;
    }

    private void RequestArtisanResume()
    {
        lastResumeRequested = true;

        if (!isTestMode)
            artisanTask.Resume();
    }

    private bool CanControlArtisan(out string errorMessage)
    {
        if (isTestMode)
        {
            errorMessage = testCanControl
                ? string.Empty
                : testControlErrorMessage ?? "Artisan IPC is unavailable.";
            return testCanControl;
        }

        return artisanTask.CanControl(out errorMessage);
    }

    private bool ShouldTakeOver()
    {
        if (isTestMode)
            return testIsBusy && testShouldTakeOver;

        artisanTask.Update();
        var snapshot = artisanTask.GetSnapshot();
        return snapshot.isBusy && snapshot.isPaused;
    }

    private bool IsArtisanPaused()
    {
        if (isTestMode)
            return testArtisanPaused;

        artisanTask.Update();
        return artisanTask.GetSnapshot().isPaused;
    }

    private bool HasConfiguredPurchases()
    {
        if (isTestMode)
            return testHasConfiguredPurchases;

        return pluginConfig.scripShopItems.Count > 0;
    }

    private bool ShouldUseNavigation()
    {
        if (isTestMode)
            return testUseNavigation;

        return true;
    }

    private bool HasNavigationCompleted()
    {
        if (isTestMode)
            return testNavigationCompleted;

        navigationTask.Update();
        return navigationTask.isCompleted;
    }

    private bool HasNavigationFailed()
    {
        if (isTestMode)
            return testNavigationFailed;

        navigationTask.Update();
        return navigationTask.hasFailed;
    }

    private string GetNavigationErrorMessage()
    {
        if (isTestMode)
            return "Navigation task failed.";

        return navigationTask.errorMessage ?? "Navigation task failed.";
    }

    private bool HasTurnInCompleted()
    {
        if (isTestMode)
            return testTurnInCompleted;

        turnInTask.Update();
        return turnInTask.isCompleted;
    }

    private bool HasTurnInFailed()
    {
        if (isTestMode)
            return testTurnInFailed;

        turnInTask.Update();
        return turnInTask.hasFailed;
    }

    private bool HasPurchaseCompleted()
    {
        if (isTestMode)
            return testPurchaseCompleted;

        purchaseTask.Update();
        return purchaseTask.isCompleted;
    }

    private bool HasPurchaseFailed()
    {
        if (isTestMode)
            return testPurchaseFailed;

        purchaseTask.Update();
        return purchaseTask.hasFailed;
    }

    private void ConsumeNavigationState()
    {
        testNavigationCompleted = false;
        testNavigationFailed = false;
    }

    private void ConsumeTurnInState()
    {
        testTurnInCompleted = false;
        testTurnInFailed = false;
    }

    private void ConsumePurchaseState()
    {
        testPurchaseCompleted = false;
        testPurchaseFailed = false;
    }

    private void Fail(string errorMessage)
    {
        lastErrorMessage = errorMessage;
        workflowMode = WorkflowMode.None;
        workflowStage = WorkflowStage.Failed;
        TryDuoLogError(errorMessage);
    }

    private static NavigationRequest BuildNavigationRequest(string reason, uint territoryId)
    {
        return new NavigationRequest(Vector3.Zero, territoryId, reason);
    }

    private static void TryDuoLogError(string message)
    {
        try
        {
            DuoLog.Error(message);
        }
        catch
        {
        }
    }

    private sealed class NoOpArtisanIpc : IArtisanIpc
    {
        public bool IsAvailable() => true;
        public bool IsListRunning() => true;
        public bool IsListPaused() => false;
        public bool IsBusy() => true;
        public bool GetStopRequest() => false;
        public void SetListPause(bool paused) { }
        public void SetStopRequest(bool stop) { }
        public void StartListById(int listId) { }
    }

    private sealed class NoOpVNavmeshIpc : IVNavmeshIpc
    {
        public bool IsAvailable() => true;
        public bool PathfindAndMoveTo(Vector3 destination, bool fly) => true;
        public bool IsPathRunning() => false;
        public void Stop() { }
    }

    private sealed class NoOpLifestreamIpc : ILifestreamIpc
    {
        public bool IsAvailable() => true;
        public bool IsBusy() => false;
        public void ExecuteCommand(string command) { }
        public void Abort() { }
        public void EnqueueInnShortcut(int? mode = null) { }
    }
}
