using System.Diagnostics;
using System.Numerics;
using StarLoom.Config;
using StarLoom.Game;
using StarLoom.Ipc;
using StarLoom.Tasks.Artisan;
using StarLoom.Tasks.Navigation;
using StarLoom.Tasks.Purchase;
using StarLoom.Tasks.Return;
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
        WaitingForPause,
        WaitingForIdle,
        NavigatingToTurnIn,
        TurnIn,
        NavigatingToPurchase,
        Purchase,
        Returning,
        ClosingGame,
        Failed,
    }

    private readonly PluginConfig pluginConfig;
    private readonly ArtisanTask artisanTask;
    private readonly NavigationTask navigationTask;
    private readonly ReturnTask returnTask;
    private readonly TurnInTask turnInTask;
    private readonly PurchaseTask purchaseTask;
    private readonly InventoryGame inventoryGame;
    private readonly PlayerStateGame playerStateGame;
    private readonly LocationGame locationGame;
    private readonly Action closeGameAction;
    private readonly Func<DateTime> getUtcNow;
    private readonly TimeSpan pauseAcknowledgementTimeout;
    private readonly TimeSpan artisanIdleTimeout;
    private readonly TimeSpan localActionReadyStableDuration;

    private WorkflowMode workflowMode;
    private WorkflowStage workflowStage;
    private bool artisanListManaged;
    private bool pendingConfiguredWorkflowStartAfterReturn;
    private bool isTestMode;
    private bool testCanControl = true;
    private bool testArtisanAvailable = true;
    private bool testArtisanListRunning = true;
    private bool testArtisanBusy = true;
    private bool testArtisanPaused;
    private bool testHasPendingPurchaseWork = true;
    private bool testHasCollectableTurnIns;
    private int testFreeSlotCount = int.MaxValue;
    private bool testIsInsideHouse = true;
    private bool testIsInsideInn;
    private bool testLocalPlayerReady = true;
    private bool testUseNavigation;
    private bool testNavigationCompleted;
    private bool testNavigationFailed;
    private bool testTurnInCompleted;
    private bool testTurnInFailed;
    private bool testPurchaseCompleted;
    private bool testPurchaseFailed;
    private bool testReturnCompleted;
    private bool testReturnFailed;
    private bool testStartConfiguredListSucceeded = true;
    private string? testControlErrorMessage;
    private DateTime pauseGateEnteredAt = DateTime.MinValue;
    private DateTime idleWaitEnteredAt = DateTime.MinValue;
    private DateTime? localActionReadyAt;

    public string currentStage => workflowStage switch
    {
        WorkflowStage.Idle => "Idle",
        WorkflowStage.MonitoringArtisan => "MonitoringArtisan",
        WorkflowStage.WaitingForPause => "WaitingForPause",
        WorkflowStage.WaitingForIdle => "WaitingForIdle",
        WorkflowStage.NavigatingToTurnIn => "NavigatingToTurnIn",
        WorkflowStage.TurnIn => "TurnIn",
        WorkflowStage.NavigatingToPurchase => "NavigatingToPurchase",
        WorkflowStage.Purchase => "Purchase",
        WorkflowStage.Returning => "Returning",
        WorkflowStage.ClosingGame => "ClosingGame",
        WorkflowStage.Failed => "Failed",
        _ => "Idle",
    };

    public bool isBusy => workflowStage is not WorkflowStage.Idle;
    public bool lastPauseRequested { get; private set; }
    public bool lastResumeRequested { get; private set; }
    public bool closeGameRequested { get; private set; }
    public string? lastErrorMessage { get; private set; }
    public NavigationRequest? lastNavigationRequest { get; private set; }

    public WorkflowTask()
        : this(
            new PluginConfig(),
            new ArtisanTask(new ArtisanIpc(), new PluginConfig()),
            new NavigationTask(new VNavmeshIpc(), new LifestreamIpc()),
            new TurnInTask(),
            new PurchaseTask(),
            new InventoryGame(),
            new PlayerStateGame(),
            new LocationGame())
    {
    }

    public WorkflowTask(
        PluginConfig pluginConfig,
        ArtisanTask artisanTask,
        NavigationTask navigationTask,
        TurnInTask turnInTask,
        PurchaseTask purchaseTask,
        InventoryGame inventoryGame,
        PlayerStateGame playerStateGame,
        LocationGame locationGame,
        ReturnTask? returnTask = null,
        Action? closeGameAction = null,
        Func<DateTime>? getUtcNow = null,
        TimeSpan? pauseAcknowledgementTimeout = null,
        TimeSpan? artisanIdleTimeout = null,
        TimeSpan? localActionReadyStableDuration = null)
    {
        this.pluginConfig = pluginConfig;
        this.artisanTask = artisanTask;
        this.navigationTask = navigationTask;
        this.turnInTask = turnInTask;
        this.purchaseTask = purchaseTask;
        this.inventoryGame = inventoryGame;
        this.playerStateGame = playerStateGame;
        this.locationGame = locationGame;
        this.returnTask = returnTask ?? new ReturnTask(navigationTask, new LifestreamIpc());
        this.closeGameAction = closeGameAction ?? (() => Process.GetCurrentProcess().Kill());
        this.getUtcNow = getUtcNow ?? (() => DateTime.UtcNow);
        this.pauseAcknowledgementTimeout = pauseAcknowledgementTimeout ?? TimeSpan.FromSeconds(5);
        this.artisanIdleTimeout = artisanIdleTimeout ?? TimeSpan.FromSeconds(15);
        this.localActionReadyStableDuration = localActionReadyStableDuration ?? TimeSpan.FromMilliseconds(500);
    }

    public static WorkflowTask CreateForTests(PluginConfig? pluginConfig = null)
    {
        pluginConfig ??= new PluginConfig
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
            new PurchaseTask(pluginConfig, new InventoryGame(), new ScripShopGame()),
            new InventoryGame(),
            new PlayerStateGame(),
            new LocationGame(),
            closeGameAction: () => { },
            getUtcNow: () => DateTime.UnixEpoch,
            localActionReadyStableDuration: TimeSpan.Zero);

        workflowTask.isTestMode = true;
        workflowTask.testArtisanAvailable = true;
        workflowTask.testArtisanListRunning = false;
        workflowTask.testArtisanBusy = false;
        workflowTask.testArtisanPaused = false;
        workflowTask.testIsInsideHouse = true;
        workflowTask.testIsInsideInn = false;
        return workflowTask;
    }

    public void StartConfiguredWorkflow()
    {
        if (isBusy)
            return;

        BeginWorkflow(WorkflowMode.ConfiguredWorkflow);

        if (!IsInsideStartLocation())
        {
            pendingConfiguredWorkflowStartAfterReturn = true;
            BeginReturn();
            return;
        }

        StartConfiguredWorkflowCore();
    }

    public void StartTurnInOnly()
    {
        if (isBusy)
            return;

        BeginWorkflow(WorkflowMode.TurnInOnly);

        if (!WorkflowStartValidator.CanStartCollectable(pluginConfig, out var errorMessage))
        {
            Fail(errorMessage);
            return;
        }

        if (ShouldUseNavigation())
        {
            BeginNavigationToTurnIn();
            return;
        }

        BeginTurnIn();
    }

    public void StartPurchaseOnly()
    {
        if (isBusy)
            return;

        BeginWorkflow(WorkflowMode.PurchaseOnly);

        if (!CanStartPurchase(out var errorMessage))
        {
            Fail(errorMessage);
            return;
        }

        if (ShouldUseNavigation())
        {
            BeginNavigationToPurchase();
            return;
        }

        BeginPurchase();
    }

    public void Stop()
    {
        AbortRuntime(stopArtisan: true);
        workflowMode = WorkflowMode.None;
        workflowStage = WorkflowStage.Idle;
        artisanListManaged = false;
        pendingConfiguredWorkflowStartAfterReturn = false;
        lastPauseRequested = false;
        lastResumeRequested = false;
        closeGameRequested = false;
        lastErrorMessage = null;
        lastNavigationRequest = null;
        pauseGateEnteredAt = DateTime.MinValue;
        idleWaitEnteredAt = DateTime.MinValue;
        localActionReadyAt = null;
    }

    public void Update()
    {
        switch (workflowStage)
        {
            case WorkflowStage.MonitoringArtisan:
                HandleMonitoringArtisan();
                return;
            case WorkflowStage.WaitingForPause:
                HandleWaitingForPause();
                return;
            case WorkflowStage.WaitingForIdle:
                HandleWaitingForIdle();
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
            WorkflowStage.WaitingForPause => "state.orchestrator.waiting_pause",
            WorkflowStage.WaitingForIdle => "state.orchestrator.waiting_idle",
            WorkflowStage.MonitoringArtisan => "state.session.monitoring",
            WorkflowStage.NavigatingToTurnIn => "state.orchestrator.running",
            WorkflowStage.TurnIn => "state.orchestrator.running",
            WorkflowStage.NavigatingToPurchase => "state.orchestrator.running",
            WorkflowStage.Purchase => "state.orchestrator.running",
            WorkflowStage.Returning => "state.orchestrator.running",
            WorkflowStage.ClosingGame => "state.orchestrator.running",
            WorkflowStage.Failed => "state.orchestrator.running",
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
        testArtisanListRunning = isBusy;
        testArtisanBusy = isBusy;
        testArtisanPaused = artisanPaused;
        testHasPendingPurchaseWork = hasConfiguredPurchases;
        testHasCollectableTurnIns = shouldTakeOver;
        testFreeSlotCount = shouldTakeOver ? Math.Max(0, pluginConfig.freeSlotThreshold - 1) : int.MaxValue;
        testUseNavigation = useNavigation;
    }

    public void SetTestArtisanState(bool isAvailable, bool isListRunning, bool isBusy, bool isPaused)
    {
        isTestMode = true;
        testArtisanAvailable = isAvailable;
        testArtisanListRunning = isListRunning;
        testArtisanBusy = isBusy;
        testArtisanPaused = isPaused;
    }

    public void SetTestInventoryState(int freeSlotCount, bool hasCollectableTurnIns, bool hasPendingPurchases)
    {
        isTestMode = true;
        testFreeSlotCount = freeSlotCount;
        testHasCollectableTurnIns = hasCollectableTurnIns;
        testHasPendingPurchaseWork = hasPendingPurchases;
    }

    public void SetTestLocation(bool isInsideHouse, bool isInsideInn)
    {
        isTestMode = true;
        testIsInsideHouse = isInsideHouse;
        testIsInsideInn = isInsideInn;
    }

    public void SetTestLocalPlayerReady(bool isReady)
    {
        isTestMode = true;
        testLocalPlayerReady = isReady;
    }

    public void SetTestUseNavigation(bool useNavigation)
    {
        isTestMode = true;
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

    public void SetTestReturnState(bool isCompleted, bool hasFailed = false)
    {
        isTestMode = true;
        testReturnCompleted = isCompleted;
        testReturnFailed = hasFailed;
    }

    public void SetTestControl(bool canControl, string errorMessage)
    {
        isTestMode = true;
        testCanControl = canControl;
        testControlErrorMessage = errorMessage;
    }

    private void BeginWorkflow(WorkflowMode mode)
    {
        workflowMode = mode;
        workflowStage = WorkflowStage.Idle;
        pendingConfiguredWorkflowStartAfterReturn = false;
        lastPauseRequested = false;
        lastResumeRequested = false;
        closeGameRequested = false;
        lastErrorMessage = null;
        lastNavigationRequest = null;
        pauseGateEnteredAt = DateTime.MinValue;
        idleWaitEnteredAt = DateTime.MinValue;
        localActionReadyAt = null;
    }

    private void StartConfiguredWorkflowCore()
    {
        if (!WorkflowStartValidator.CanStartCollectable(pluginConfig, out var errorMessage))
        {
            Fail(errorMessage);
            return;
        }

        var snapshot = GetArtisanSnapshot();
        if (!CanStartArtisanList(snapshot, out errorMessage))
        {
            Fail(errorMessage);
            return;
        }

        if ((!artisanListManaged || !snapshot.isListRunning) && !StartConfiguredList())
        {
            Fail("Failed to start configured Artisan list.");
            return;
        }

        artisanListManaged = true;
        workflowStage = WorkflowStage.MonitoringArtisan;
    }

    private void HandleMonitoringArtisan()
    {
        if (workflowMode != WorkflowMode.ConfiguredWorkflow)
            return;

        if (ShouldFinalizeConfiguredWorkflow())
        {
            DispatchFinalCompletion();
            return;
        }

        if (!ShouldTakeOverForTurnInAndPurchase())
            return;

        EnterPauseGate();
    }

    private void HandleWaitingForPause()
    {
        var status = BuildArtisanPauseStatus();
        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForAcknowledgement,
            status,
            getUtcNow() - pauseGateEnteredAt,
            pauseAcknowledgementTimeout,
            artisanIdleTimeout);

        switch (decision.kind)
        {
            case ArtisanPauseDecisionKind.MoveToIdleWait:
                idleWaitEnteredAt = getUtcNow();
                localActionReadyAt = null;
                workflowStage = WorkflowStage.WaitingForIdle;
                return;
            case ArtisanPauseDecisionKind.Fail:
                Fail(decision.errorMessage ?? "Timed out while waiting for Artisan pause acknowledgement.");
                return;
            default:
                return;
        }
    }

    private void HandleWaitingForIdle()
    {
        var status = BuildArtisanPauseStatus();
        if (ArtisanPauseGate.HasPauseAcknowledgement(status))
        {
            if (IsLocalPlayerReady())
            {
                if (localActionReadyAt is null)
                    localActionReadyAt = getUtcNow();

                if ((getUtcNow() - localActionReadyAt.Value) >= localActionReadyStableDuration)
                {
                    localActionReadyAt = null;

                    if (ShouldUseNavigation())
                    {
                        BeginNavigationToTurnIn();
                        return;
                    }

                    BeginTurnIn();
                    return;
                }
            }
            else
            {
                localActionReadyAt = null;
            }
        }

        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForIdle,
            status,
            getUtcNow() - idleWaitEnteredAt,
            pauseAcknowledgementTimeout,
            artisanIdleTimeout);

        if (decision.kind == ArtisanPauseDecisionKind.Fail)
            Fail(decision.errorMessage ?? "Timed out while waiting for local control after pausing Artisan.");
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

        if (workflowMode == WorkflowMode.ConfiguredWorkflow)
        {
            if (HasPendingPurchaseWork())
            {
                if (ShouldUseNavigation())
                {
                    BeginNavigationToPurchase();
                    return;
                }

                BeginPurchase();
                return;
            }

            DispatchFinalCompletion();
            return;
        }

        CompleteStandaloneWorkflow();
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

        if (workflowMode is WorkflowMode.ConfiguredWorkflow or WorkflowMode.PurchaseOnly)
        {
            DispatchFinalCompletion();
            return;
        }

        CompleteStandaloneWorkflow();
    }

    private void HandleReturning()
    {
        if (HasReturnFailed())
        {
            Fail(GetReturnErrorMessage());
            ConsumeReturnState();
            return;
        }

        if (!HasReturnCompleted())
            return;

        ConsumeReturnState();

        if (pendingConfiguredWorkflowStartAfterReturn)
        {
            pendingConfiguredWorkflowStartAfterReturn = false;

            if (!IsInsideStartLocation())
            {
                Fail("Failed to return to the configured start location.");
                return;
            }

            StartConfiguredWorkflowCore();
            return;
        }

        CompletePostActionWorkflow();
    }

    private void EnterPauseGate()
    {
        lastPauseRequested = true;
        pauseGateEnteredAt = getUtcNow();
        idleWaitEnteredAt = DateTime.MinValue;
        localActionReadyAt = null;

        if (!isTestMode)
            artisanTask.RequestPause();

        workflowStage = WorkflowStage.WaitingForPause;
    }

    private void BeginNavigationToTurnIn()
    {
        lastNavigationRequest = BuildTurnInNavigationRequest();

        if (!isTestMode)
            navigationTask.Start(lastNavigationRequest.Value);

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
        lastNavigationRequest = BuildPurchaseNavigationRequest();

        if (!isTestMode)
            navigationTask.Start(lastNavigationRequest.Value);

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
        if (pluginConfig.defaultReturnPoint == null)
        {
            Fail("A return point must be configured before continuing.");
            return;
        }

        if (!isTestMode)
        {
            returnTask.Start(pluginConfig.defaultReturnPoint);
            if (returnTask.hasFailed)
            {
                Fail(returnTask.errorMessage ?? "Return task failed.");
                return;
            }
        }

        workflowStage = WorkflowStage.Returning;
    }

    private void BeginCloseGame()
    {
        closeGameRequested = true;
        workflowStage = WorkflowStage.ClosingGame;

        if (!isTestMode)
            closeGameAction();
    }

    private void DispatchFinalCompletion()
    {
        if (pluginConfig.postPurchaseAction == PostPurchaseAction.CloseGame)
        {
            BeginCloseGame();
            return;
        }

        BeginReturn();
    }

    private bool CanStartPurchase(out string errorMessage)
    {
        if (isTestMode)
        {
            if (pluginConfig.scripShopItems is not { Count: > 0 })
            {
                errorMessage = "The purchase list is empty.";
                return false;
            }

            if (!pluginConfig.scripShopItems.Any(item => item.itemId > 0 && item.targetCount > 0))
            {
                errorMessage = "The purchase list is empty or has no valid target quantities.";
                return false;
            }

            if (!testHasPendingPurchaseWork)
            {
                errorMessage = "All configured purchase items already reached their target quantities.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        return WorkflowStartValidator.CanStartPurchase(pluginConfig, inventoryGame, out errorMessage);
    }

    private bool CanStartArtisanList(ArtisanSnapshot snapshot, out string errorMessage)
    {
        if (isTestMode && !testCanControl)
        {
            errorMessage = testControlErrorMessage ?? "Artisan IPC is unavailable.";
            return false;
        }

        return WorkflowStartValidator.CanStartArtisanList(snapshot, artisanListManaged, out errorMessage);
    }

    private ArtisanSnapshot GetArtisanSnapshot()
    {
        if (isTestMode)
        {
            return new ArtisanSnapshot(
                testArtisanAvailable,
                testArtisanListRunning,
                testArtisanPaused,
                false,
                testArtisanBusy,
                pluginConfig.artisanListId);
        }

        artisanTask.Update();
        return artisanTask.GetSnapshot();
    }

    private ArtisanPauseStatus BuildArtisanPauseStatus()
    {
        var snapshot = GetArtisanSnapshot();
        return new ArtisanPauseStatus(
            snapshot.isBusy,
            snapshot.isListRunning,
            snapshot.isPaused,
            snapshot.hasStopRequest);
    }

    private bool IsInsideStartLocation()
    {
        if (isTestMode)
            return testIsInsideHouse || testIsInsideInn;

        return locationGame.IsInsideHouse() || locationGame.IsInsideInn();
    }

    private bool HasPendingPurchaseWork()
    {
        if (isTestMode)
            return testHasPendingPurchaseWork;

        return WorkflowStartValidator.HasPendingPurchaseWork(pluginConfig, inventoryGame);
    }

    private bool HasTurnInWork()
    {
        if (isTestMode)
            return testHasCollectableTurnIns;

        return inventoryGame.HasCollectableTurnIns();
    }

    private bool IsBelowFreeSlotThreshold()
    {
        var threshold = pluginConfig.freeSlotThreshold;
        if (threshold <= 0)
            return false;

        var freeSlotCount = isTestMode
            ? testFreeSlotCount
            : inventoryGame.GetFreeSlotCount();

        return freeSlotCount < threshold;
    }

    private bool ShouldTakeOverForTurnInAndPurchase()
    {
        var snapshot = GetArtisanSnapshot();
        return artisanListManaged
            && snapshot.isAvailable
            && IsBelowFreeSlotThreshold()
            && HasTurnInWork();
    }

    private bool ShouldFinalizeConfiguredWorkflow()
    {
        var snapshot = GetArtisanSnapshot();
        return !HasPendingPurchaseWork()
            || (!snapshot.isListRunning && !ShouldTakeOverForTurnInAndPurchase());
    }

    private bool ShouldUseNavigation()
    {
        if (isTestMode)
            return testUseNavigation;

        return true;
    }

    private bool StartConfiguredList()
    {
        if (isTestMode)
            return testStartConfiguredListSucceeded;

        return artisanTask.StartConfiguredList();
    }

    private bool IsLocalPlayerReady()
    {
        if (isTestMode)
            return testLocalPlayerReady;

        return LocalPlayerActionGate.IsReadyForAutomation(playerStateGame.GetLocalPlayerActionStatus());
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

    private bool HasReturnCompleted()
    {
        if (isTestMode)
            return testReturnCompleted;

        returnTask.Update();
        return returnTask.isCompleted;
    }

    private bool HasReturnFailed()
    {
        if (isTestMode)
            return testReturnFailed;

        returnTask.Update();
        return returnTask.hasFailed;
    }

    private string GetReturnErrorMessage()
    {
        if (isTestMode)
            return "Return task failed.";

        return returnTask.errorMessage ?? "Return task failed.";
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

    private void ConsumeReturnState()
    {
        testReturnCompleted = false;
        testReturnFailed = false;
    }

    private void CompletePostActionWorkflow()
    {
        AbortRuntime(stopArtisan: false);
        artisanListManaged = false;
        workflowMode = WorkflowMode.None;
        workflowStage = WorkflowStage.Idle;
        pendingConfiguredWorkflowStartAfterReturn = false;
        localActionReadyAt = null;
        pauseGateEnteredAt = DateTime.MinValue;
        idleWaitEnteredAt = DateTime.MinValue;
    }

    private void CompleteStandaloneWorkflow()
    {
        AbortRuntime(stopArtisan: false);
        workflowMode = WorkflowMode.None;
        workflowStage = WorkflowStage.Idle;
        pendingConfiguredWorkflowStartAfterReturn = false;
        localActionReadyAt = null;
        pauseGateEnteredAt = DateTime.MinValue;
        idleWaitEnteredAt = DateTime.MinValue;
    }

    private void AbortRuntime(bool stopArtisan)
    {
        returnTask.Stop();
        navigationTask.Stop();
        turnInTask.Stop();
        purchaseTask.Stop();

        if (stopArtisan)
            artisanTask.Stop();
    }

    private void Fail(string errorMessage)
    {
        AbortRuntime(stopArtisan: true);
        lastErrorMessage = errorMessage;
        workflowMode = WorkflowMode.None;
        workflowStage = WorkflowStage.Failed;
        artisanListManaged = false;
        pendingConfiguredWorkflowStartAfterReturn = false;
        localActionReadyAt = null;
        pauseGateEnteredAt = DateTime.MinValue;
        idleWaitEnteredAt = DateTime.MinValue;
        TryDuoLogError(errorMessage);
    }

    private NavigationRequest BuildTurnInNavigationRequest()
    {
        var shop = pluginConfig.preferredCollectableShop
            ?? throw new InvalidOperationException("A collectable shop must be configured before starting.");

        return new NavigationRequest(
            shop.location,
            shop.territoryId,
            "collectable",
            shop.aetheryteId,
            2f,
            shop.isLifestreamRequired,
            shop.lifestreamCommand);
    }

    private NavigationRequest BuildPurchaseNavigationRequest()
    {
        var shop = pluginConfig.preferredCollectableShop
            ?? throw new InvalidOperationException("A collectable shop must be configured before starting.");

        return new NavigationRequest(
            shop.scripShopLocation,
            shop.territoryId,
            "purchase",
            shop.aetheryteId,
            0.4f,
            shop.isLifestreamRequired,
            shop.lifestreamCommand);
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
