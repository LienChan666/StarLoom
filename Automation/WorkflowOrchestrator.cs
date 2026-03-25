using System;
using Starloom.Services;

namespace Starloom.Automation;

internal sealed class WorkflowOrchestrator : IDisposable
{
    private enum PendingStartKind
    {
        None,
        ConfiguredWorkflow,
        CollectableTurnIn,
        PurchaseOnly,
    }

    private readonly WorkflowTaskDispatcher dispatcher = new();
    private PendingStartKind pendingStart;

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

        if (!PrepareStartOrReturn(PendingStartKind.CollectableTurnIn))
            return;

        StartCollectableTurnInCore();
    }

    internal void StartPurchaseOnly()
    {
        if (IsBusy) return;

        if (!PrepareStartOrReturn(PendingStartKind.PurchaseOnly))
            return;

        StartPurchaseOnlyCore();
    }

    internal void Stop()
    {
        CleanupRuntime(stopArtisan: true);
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

        if (!P.TM.IsBusy && State is WorkflowState.Running or WorkflowState.ReturningToCraftPoint)
            State = WorkflowState.Idle;
    }

    internal string GetStateKey()
    {
        return State switch
        {
            WorkflowState.Idle => "state.orchestrator.idle",
            WorkflowState.WaitingForStartReturn => "state.orchestrator.running",
            WorkflowState.StartingArtisan => "state.orchestrator.running",
            WorkflowState.Running => "state.orchestrator.running",
            WorkflowState.ReturningToCraftPoint => "state.orchestrator.running",
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
            case PendingStartKind.CollectableTurnIn:
                StartCollectableTurnInCore();
                return;
            case PendingStartKind.PurchaseOnly:
                StartPurchaseOnlyCore();
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

        State = WorkflowState.StartingArtisan;
        dispatcher.DispatchConfiguredWorkflow();
        State = WorkflowState.Running;
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

    private void Fail()
    {
        CleanupRuntime(stopArtisan: true);
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
