using Starloom.GameInterop.IPC;
using Starloom.Tasks;
using System;

namespace Starloom.Automation;

public enum ArtisanSessionState
{
    Idle,
    WaitingForPreStartJobs,
    WaitingForArtisanStart,
    Monitoring,
    WaitingForThresholdJobs,
    Failed,
}

internal sealed class ArtisanSession
{
    private DateTime stateEnteredAt = DateTime.MinValue;
    private bool warnedMissingCollectablesAtThreshold;

    internal ArtisanSessionState State { get; private set; } = ArtisanSessionState.Idle;
    internal bool IsActive => State is not ArtisanSessionState.Idle and not ArtisanSessionState.Failed;

    internal string GetStateKey()
        => State switch
        {
            ArtisanSessionState.Idle => "state.session.idle",
            ArtisanSessionState.WaitingForPreStartJobs => "state.session.pre_start",
            ArtisanSessionState.WaitingForArtisanStart => "state.session.waiting_start",
            ArtisanSessionState.Monitoring => "state.session.monitoring",
            ArtisanSessionState.WaitingForThresholdJobs => "state.session.threshold_jobs",
            ArtisanSessionState.Failed => "state.session.failed",
            _ => "state.session.idle",
        };

    internal bool TryStart()
    {
        if (IsActive || P.TM.IsBusy)
        {
            SetFailure("A managed Artisan session is already running.", stopArtisan: false);
            return false;
        }

        if (!P.Artisan.IsAvailable())
        {
            SetFailure("Artisan is not available.", stopArtisan: false);
            return false;
        }

        if (C.ArtisanListId <= 0)
        {
            SetFailure("Artisan list id is required.", stopArtisan: false);
            return false;
        }

        if (P.Artisan.IsListRunning() || P.Artisan.GetEnduranceStatus() || P.Artisan.IsBusy())
        {
            SetFailure("Artisan is busy with another task.", stopArtisan: false);
            return false;
        }

        warnedMissingCollectablesAtThreshold = false;

        if (P.Artisan.GetStopRequest())
            P.Artisan.SetStopRequest(false);

        if (!IsBelowFreeSlotThreshold())
            return TryStartArtisanList();

        if (!P.Inventory.HasCollectableTurnIns())
        {
            SetFailure("Inventory is below the free-slot threshold and there are no turn-ins available.", stopArtisan: false);
            return false;
        }

        Workflows.EnqueueConfiguredWorkflow();
        TransitionTo(ArtisanSessionState.WaitingForPreStartJobs);
        return true;
    }

    internal void Stop()
    {
        P.TM.Abort();

        if (P.Artisan.IsAvailable() && (P.Artisan.IsListRunning() || P.Artisan.GetEnduranceStatus() || P.Artisan.IsBusy()))
            P.Artisan.SetStopRequest(true);

        warnedMissingCollectablesAtThreshold = false;
        TransitionTo(ArtisanSessionState.Idle);
    }

    internal void Update()
    {
        switch (State)
        {
            case ArtisanSessionState.Idle:
            case ArtisanSessionState.Failed:
                return;
            case ArtisanSessionState.WaitingForPreStartJobs:
                UpdatePreStartJobs();
                return;
            case ArtisanSessionState.WaitingForArtisanStart:
                UpdateWaitingForArtisanStart();
                return;
            case ArtisanSessionState.Monitoring:
                UpdateMonitoring();
                return;
            case ArtisanSessionState.WaitingForThresholdJobs:
                UpdateThresholdJobs();
                return;
        }
    }

    private void UpdatePreStartJobs()
    {
        if (P.TM.IsBusy) return;
        TryStartArtisanList();
    }

    private void UpdateWaitingForArtisanStart()
    {
        if (P.Artisan.IsListRunning())
        {
            warnedMissingCollectablesAtThreshold = false;
            TransitionTo(ArtisanSessionState.Monitoring);
            return;
        }

        if (TimedOut(8))
            SetFailure("Timed out while waiting for Artisan to start the list.", stopArtisan: false);
    }

    private void UpdateMonitoring()
    {
        if (P.TM.IsBusy)
        {
            TransitionTo(ArtisanSessionState.WaitingForThresholdJobs);
            return;
        }

        if (!P.Artisan.IsListRunning())
        {
            TransitionTo(ArtisanSessionState.Idle);
            return;
        }

        if (!IsBelowFreeSlotThreshold())
        {
            warnedMissingCollectablesAtThreshold = false;
            return;
        }

        if (!P.Inventory.HasCollectableTurnIns())
        {
            if (!warnedMissingCollectablesAtThreshold)
                warnedMissingCollectablesAtThreshold = true;
            return;
        }

        Workflows.EnqueueConfiguredWorkflow();
        warnedMissingCollectablesAtThreshold = false;
        TransitionTo(ArtisanSessionState.WaitingForThresholdJobs);
    }

    private void UpdateThresholdJobs()
    {
        if (P.TM.IsBusy) return;

        if (IsBelowFreeSlotThreshold() && P.Inventory.HasCollectableTurnIns())
        {
            Workflows.EnqueueConfiguredWorkflow();
            TransitionTo(ArtisanSessionState.WaitingForThresholdJobs);
            return;
        }

        TransitionTo(ArtisanSessionState.Monitoring);
    }

    private bool TryStartArtisanList()
    {
        if (C.ArtisanListId <= 0)
        {
            SetFailure("Artisan list id is required.", stopArtisan: false);
            return false;
        }

        if (!WorkflowStartValidator.CanStartArtisanList(out var errorMessage))
        {
            SetFailure(errorMessage, stopArtisan: false);
            return false;
        }

        if (P.Artisan.GetStopRequest())
            P.Artisan.SetStopRequest(false);

        P.Artisan.StartListById(C.ArtisanListId);
        TransitionTo(ArtisanSessionState.WaitingForArtisanStart);
        return true;
    }

    private bool IsBelowFreeSlotThreshold()
        => C.FreeSlotThreshold > 0 && P.Inventory.GetFreeSlotCount() < C.FreeSlotThreshold;

    private void SetFailure(string message, bool stopArtisan)
    {
        if (stopArtisan && P.Artisan.IsAvailable() && (P.Artisan.IsListRunning() || P.Artisan.GetEnduranceStatus() || P.Artisan.IsBusy()))
            P.Artisan.SetStopRequest(true);

        Svc.Log.Error($"Managed session failed: {message}");
        TransitionTo(ArtisanSessionState.Failed);
    }

    private void TransitionTo(ArtisanSessionState newState)
    {
        State = newState;
        stateEnteredAt = DateTime.UtcNow;
        Svc.Log.Debug($"Managed session -> {newState}");
    }

    private bool TimedOut(int seconds)
        => (DateTime.UtcNow - stateEnteredAt) > TimeSpan.FromSeconds(seconds);
}
