using ECommons.DalamudServices;
using StarLoom.Core;
using StarLoom.IPC;
using StarLoom.Workflows;
using System;
using System.Collections.Generic;

namespace StarLoom.Services;

public enum ManagedArtisanSessionState
{
    Idle,
    WaitingForPreStartJobs,
    WaitingForArtisanStart,
    Monitoring,
    WaitingForThresholdJobs,
    Failed,
}

public sealed class ManagedArtisanSession
{
    private readonly IArtisanIpc _artisan;
    private readonly JobOrchestrator _orchestrator;
    private readonly Configuration _config;
    private readonly Func<IReadOnlyList<IAutomationJob>> _jobFactory;
    private readonly WorkflowStartValidator _workflowValidator;

    private DateTime _stateEnteredAt = DateTime.MinValue;
    private bool _warnedMissingCollectablesAtThreshold;

    public ManagedArtisanSessionState State { get; private set; } = ManagedArtisanSessionState.Idle;
    public bool IsActive => State is not ManagedArtisanSessionState.Idle and not ManagedArtisanSessionState.Failed;

    public ManagedArtisanSession(
        IArtisanIpc artisan,
        JobOrchestrator orchestrator,
        Configuration config,
        Func<IReadOnlyList<IAutomationJob>> jobFactory,
        WorkflowStartValidator workflowValidator)
    {
        _artisan = artisan;
        _orchestrator = orchestrator;
        _config = config;
        _jobFactory = jobFactory;
        _workflowValidator = workflowValidator;
    }

    public string GetStateKey()
        => State switch
        {
            ManagedArtisanSessionState.Idle => "state.session.idle",
            ManagedArtisanSessionState.WaitingForPreStartJobs => "state.session.pre_start",
            ManagedArtisanSessionState.WaitingForArtisanStart => "state.session.waiting_start",
            ManagedArtisanSessionState.Monitoring => "state.session.monitoring",
            ManagedArtisanSessionState.WaitingForThresholdJobs => "state.session.threshold_jobs",
            ManagedArtisanSessionState.Failed => "state.session.failed",
            _ => "state.session.idle",
        };

    public bool TryStart()
    {
        if (IsActive || _orchestrator.IsRunning)
        {
            SetFailure("A managed Artisan session is already running.", stopArtisan: false);
            return false;
        }

        if (!_artisan.IsAvailable())
        {
            SetFailure("Artisan is not available.", stopArtisan: false);
            return false;
        }

        if (_config.ArtisanListId <= 0)
        {
            SetFailure("Artisan list id is required.", stopArtisan: false);
            return false;
        }

        if (_artisan.IsListRunning() || _artisan.GetEnduranceStatus() || _artisan.IsBusy())
        {
            SetFailure("Artisan is busy with another task.", stopArtisan: false);
            return false;
        }

        _warnedMissingCollectablesAtThreshold = false;

        if (_artisan.GetStopRequest())
            _artisan.SetStopRequest(false);

        if (!IsBelowFreeSlotThreshold())
            return TryStartArtisanList();

        if (!InventoryService.HasCollectableTurnIns())
        {
            SetFailure("Inventory is below the free-slot threshold and there are no turn-ins available.", stopArtisan: false);
            return false;
        }

        if (!_orchestrator.TryStart(_jobFactory()))
        {
            SetFailure("Failed to start the Starloom pre-start workflow.", stopArtisan: false);
            return false;
        }

        TransitionTo(ManagedArtisanSessionState.WaitingForPreStartJobs);
        return true;
    }

    public void Stop()
    {
        if (_orchestrator.IsRunning)
            _orchestrator.Abort();

        if (_artisan.IsAvailable() && (_artisan.IsListRunning() || _artisan.GetEnduranceStatus() || _artisan.IsBusy()))
            _artisan.SetStopRequest(true);

        _warnedMissingCollectablesAtThreshold = false;
        TransitionTo(ManagedArtisanSessionState.Idle);
    }

    public void Update()
    {
        switch (State)
        {
            case ManagedArtisanSessionState.Idle:
            case ManagedArtisanSessionState.Failed:
                return;

            case ManagedArtisanSessionState.WaitingForPreStartJobs:
                UpdatePreStartJobs();
                return;

            case ManagedArtisanSessionState.WaitingForArtisanStart:
                UpdateWaitingForArtisanStart();
                return;

            case ManagedArtisanSessionState.Monitoring:
                UpdateMonitoring();
                return;

            case ManagedArtisanSessionState.WaitingForThresholdJobs:
                UpdateThresholdJobs();
                return;
        }
    }

    private void UpdatePreStartJobs()
    {
        if (_orchestrator.IsRunning)
            return;

        if (_orchestrator.State == OrchestratorState.Failed)
        {
            SetFailure("Pre-start turn-in workflow failed.", stopArtisan: false);
            return;
        }

        if (_orchestrator.State == OrchestratorState.Completed)
            TryStartArtisanList();
    }

    private void UpdateWaitingForArtisanStart()
    {
        if (_artisan.IsListRunning())
        {
            _warnedMissingCollectablesAtThreshold = false;
            TransitionTo(ManagedArtisanSessionState.Monitoring);
            return;
        }

        if (TimedOut(8))
            SetFailure("Timed out while waiting for Artisan to start the list.", stopArtisan: false);
    }

    private void UpdateMonitoring()
    {
        if (_orchestrator.IsRunning)
        {
            TransitionTo(ManagedArtisanSessionState.WaitingForThresholdJobs);
            return;
        }

        if (!_artisan.IsListRunning())
        {
            TransitionTo(ManagedArtisanSessionState.Idle);
            return;
        }

        if (!IsBelowFreeSlotThreshold())
        {
            _warnedMissingCollectablesAtThreshold = false;
            return;
        }

        if (!InventoryService.HasCollectableTurnIns())
        {
            if (!_warnedMissingCollectablesAtThreshold)
            {
                _warnedMissingCollectablesAtThreshold = true;
            }

            return;
        }

        if (!_orchestrator.TryStart(_jobFactory()))
        {
            SetFailure("Failed to start the low-space handoff workflow.", stopArtisan: true);
            return;
        }

        _warnedMissingCollectablesAtThreshold = false;
        TransitionTo(ManagedArtisanSessionState.WaitingForThresholdJobs);
    }

    private void UpdateThresholdJobs()
    {
        if (_orchestrator.IsRunning)
            return;

        if (_orchestrator.State == OrchestratorState.Failed)
        {
            SetFailure("Turn-in workflow failed.", stopArtisan: true);
            return;
        }

        if (_orchestrator.State == OrchestratorState.Completed && IsBelowFreeSlotThreshold() && InventoryService.HasCollectableTurnIns())
        {
            if (!_orchestrator.TryStart(_jobFactory()))
            {
                SetFailure("Failed to continue the low-space handoff workflow.", stopArtisan: true);
                return;
            }

            TransitionTo(ManagedArtisanSessionState.WaitingForThresholdJobs);
            return;
        }

        if (_orchestrator.State == OrchestratorState.Completed)
            TransitionTo(ManagedArtisanSessionState.Monitoring);
    }

    private bool TryStartArtisanList()
    {
        if (_config.ArtisanListId <= 0)
        {
            SetFailure("Artisan list id is required.", stopArtisan: false);
            return false;
        }

        if (!_workflowValidator.CanStartArtisanList(out var errorMessage))
        {
            SetFailure(errorMessage, stopArtisan: false);
            return false;
        }

        if (_artisan.GetStopRequest())
            _artisan.SetStopRequest(false);

        _artisan.StartListById(_config.ArtisanListId);
        TransitionTo(ManagedArtisanSessionState.WaitingForArtisanStart);
        return true;
    }

    private bool IsBelowFreeSlotThreshold()
        => _config.FreeSlotThreshold > 0 && InventoryService.GetFreeSlotCount() < _config.FreeSlotThreshold;

    private void SetFailure(string message, bool stopArtisan)
    {
        if (stopArtisan && _artisan.IsAvailable() && (_artisan.IsListRunning() || _artisan.GetEnduranceStatus() || _artisan.IsBusy()))
            _artisan.SetStopRequest(true);

        Svc.Log.Error($"[Starloom] Managed session failed: {message}");
        TransitionTo(ManagedArtisanSessionState.Failed);
    }

    private void TransitionTo(ManagedArtisanSessionState newState)
    {
        State = newState;
        _stateEnteredAt = DateTime.UtcNow;

        Svc.Log.Info($"[Starloom] Managed session -> {newState}");
    }

    private bool TimedOut(int seconds)
        => (DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(seconds);
}
