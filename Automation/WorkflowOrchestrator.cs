using System;
using Starloom.Services;

namespace Starloom.Automation;

internal sealed class WorkflowOrchestrator : IDisposable
{
    private enum PendingStartKind
    {
        None,
        ConfiguredWorkflow,
    }

    private readonly WorkflowTaskDispatcher dispatcher = new();
    private PendingStartKind pendingStart;
    private bool artisanListManaged;

    internal WorkflowState State { get; private set; } = WorkflowState.Idle;
    internal bool IsBusy => State is not WorkflowState.Idle || P.TM.IsBusy;
    internal bool HasConfiguredPurchases => C.ScripShopItems is { Count: > 0 };

    internal void StartConfiguredWorkflow()
    {
        if (IsBusy) return;

        if (!PrepareStartOrReturn(PendingStartKind.ConfiguredWorkflow))
            return;

        StartConfiguredWorkflowCore();
    }

    internal void StartCollectableTurnIn()
    {
        if (IsBusy) return;

        StartCollectableTurnInCore();
    }

    internal void StartPurchaseOnly()
    {
        if (IsBusy) return;

        StartPurchaseOnlyCore();
    }

    internal void Stop()
    {
        CleanupRuntime(stopArtisan: true);
        artisanListManaged = false;
        pendingStart = PendingStartKind.None;
        State = WorkflowState.Idle;
    }

    internal void Update()
    {
        if (!P.TM.IsBusy && State == WorkflowState.WaitingForStartReturn)
        {
            ResumePendingStartAfterReturn();
            return;
        }

        if (P.TM.IsBusy)
            return;

        switch (State)
        {
            case WorkflowState.MonitoringArtisan:
                HandleMonitoringArtisan();
                return;
            case WorkflowState.LoopingTurnInAndPurchase:
                HandleLoopingTurnInAndPurchase();
                return;
            case WorkflowState.FinalizingCompletion:
                CompleteFinalizingCompletion();
                return;
            case WorkflowState.Running:
                State = WorkflowState.Idle;
                return;
        }
    }

    internal string GetStateKey()
    {
        return State switch
        {
            WorkflowState.Idle => "state.orchestrator.idle",
            WorkflowState.WaitingForStartReturn => "state.orchestrator.running",
            WorkflowState.MonitoringArtisan => "state.orchestrator.running",
            WorkflowState.LoopingTurnInAndPurchase => "state.orchestrator.running",
            WorkflowState.FinalizingCompletion => "state.orchestrator.running",
            WorkflowState.Running => "state.orchestrator.running",
            WorkflowState.Failed => "state.orchestrator.running",
            _ => "state.orchestrator.idle",
        };
    }

    private bool PrepareStartOrReturn(PendingStartKind startKind)
    {
        if (!IsInsideStartLocation())
        {
            pendingStart = startKind;
            dispatcher.DispatchStartReturn();
            State = WorkflowState.WaitingForStartReturn;
            return false;
        }

        pendingStart = PendingStartKind.None;
        return true;
    }

    private void ResumePendingStartAfterReturn()
    {
        if (!IsInsideStartLocation())
        {
            Fail();
            return;
        }

        var nextStart = pendingStart;
        pendingStart = PendingStartKind.None;

        switch (nextStart)
        {
            case PendingStartKind.ConfiguredWorkflow:
                StartConfiguredWorkflowCore();
                return;
            default:
                State = WorkflowState.Idle;
                return;
        }
    }

    private void StartConfiguredWorkflowCore()
    {
        if (!WorkflowStartValidator.CanStartCollectable(out var error))
        {
            DuoLog.Error(error);
            Fail();
            return;
        }

        if (!WorkflowStartValidator.CanStartArtisanList(artisanListManaged, out error))
        {
            DuoLog.Error(error);
            Fail();
            return;
        }

        dispatcher.DispatchConfiguredWorkflow(artisanListManaged);
        artisanListManaged = true;
        State = WorkflowState.MonitoringArtisan;
    }

    private void StartCollectableTurnInCore()
    {
        if (!WorkflowStartValidator.CanStartCollectable(out var error))
        {
            DuoLog.Error(error);
            Fail();
            return;
        }

        dispatcher.DispatchCollectableTurnIn();
        State = WorkflowState.Running;
    }

    private void StartPurchaseOnlyCore()
    {
        if (!WorkflowStartValidator.CanStartPurchase(out var error))
        {
            DuoLog.Error(error);
            Fail();
            return;
        }

        dispatcher.DispatchPurchaseOnly();
        State = WorkflowState.Running;
    }

    private static bool IsInsideStartLocation()
    {
        return HousingReturnPointService.IsInsideHouse() || HousingReturnPointService.IsInsideInn();
    }

    private void HandleMonitoringArtisan()
    {
        if (ShouldFinalizeConfiguredWorkflow())
        {
            FinalizeConfiguredWorkflow();
            return;
        }

        if (ShouldTakeOverForTurnInAndPurchase())
        {
            EnterTurnInAndPurchaseLoop();
            return;
        }
    }

    private void HandleLoopingTurnInAndPurchase()
    {
        if (ShouldFinalizeLoopAfterTurnInAndPurchase())
        {
            FinalizeConfiguredWorkflow();
            return;
        }

        if (ShouldTakeOverForTurnInAndPurchase())
        {
            EnterTurnInAndPurchaseLoop();
            return;
        }

        ResumeCraftingForNextCycle();
    }

    private void CompleteFinalizingCompletion()
    {
        State = WorkflowState.Idle;
    }

    private void EnterTurnInAndPurchaseLoop()
    {
        dispatcher.DispatchLoopTurnInAndPurchase();
        State = WorkflowState.LoopingTurnInAndPurchase;
    }

    private void ResumeCraftingForNextCycle()
    {
        if (!PrepareStartOrReturn(PendingStartKind.ConfiguredWorkflow))
            return;

        StartConfiguredWorkflowCore();
    }

    private void FinalizeConfiguredWorkflow()
    {
        artisanListManaged = false;
        dispatcher.DispatchFinalCompletion();
        State = WorkflowState.FinalizingCompletion;
    }

    private static bool ShouldTakeOverForTurnInAndPurchase()
    {
        return IsBelowFreeSlotThreshold() && P.Inventory.HasCollectableTurnIns();
    }

    private static bool IsBelowFreeSlotThreshold()
    {
        return C.FreeSlotThreshold > 0 && P.Inventory.GetFreeSlotCount() < C.FreeSlotThreshold;
    }

    private static bool HasPendingPurchaseWorkRemaining()
    {
        return P.PurchaseResolver.HasPending();
    }

    private static bool ShouldFinalizeLoopAfterTurnInAndPurchase()
    {
        return !HasPendingPurchaseWorkRemaining();
    }

    private static bool ShouldFinalizeConfiguredWorkflow()
    {
        return !HasPendingPurchaseWorkRemaining()
            || (!P.Artisan.IsListRunning() && !ShouldTakeOverForTurnInAndPurchase());
    }

    private void Fail()
    {
        CleanupRuntime(stopArtisan: true);
        artisanListManaged = false;
        pendingStart = PendingStartKind.None;
        State = WorkflowState.Failed;
    }

    private static void CleanupRuntime(bool stopArtisan)
    {
        P.TM.Abort();
        P.Navigation.Stop();
        P.CollectableTurnIn.Stop();
        P.ScripPurchase.Stop();

        if (stopArtisan && P.Artisan.IsAvailable() && (P.Artisan.IsListRunning() || P.Artisan.GetEnduranceStatus() || P.Artisan.IsBusy()))
            P.Artisan.SetStopRequest(true);
    }

    public void Dispose()
    {
        if (IsBusy)
            Stop();
    }
}
