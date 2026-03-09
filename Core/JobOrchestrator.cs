using ECommons.DalamudServices;
using StarLoom.IPC;
using System;
using System.Collections.Generic;

namespace StarLoom.Core;

public enum OrchestratorState
{
    Idle,
    WaitingForArtisanPause,
    WaitingForArtisanIdle,
    RunningJobs,
    Completed,
    Failed,
}

public sealed class JobOrchestrator : IDisposable
{
    private static readonly TimeSpan PauseAcknowledgementTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ArtisanIdleTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LocalActionReadyStableDuration = TimeSpan.FromMilliseconds(500);

    private readonly IArtisanIpc _artisan;
    private readonly JobContext _context;
    private readonly Queue<IAutomationJob> _pendingJobs = new();

    private DateTime _stateEnteredAt = DateTime.MinValue;
    private DateTime? _localActionReadyAt;

    public IAutomationJob? CurrentJob { get; private set; }
    public OrchestratorState State { get; private set; } = OrchestratorState.Idle;
    public bool IsRunning => State is OrchestratorState.WaitingForArtisanPause or OrchestratorState.WaitingForArtisanIdle or OrchestratorState.RunningJobs;

    public JobOrchestrator(IArtisanIpc artisan, JobContext context)
    {
        _artisan = artisan;
        _context = context;
    }

    public void Enqueue(IAutomationJob job) => _pendingJobs.Enqueue(job);

    public bool TryStart(IEnumerable<IAutomationJob> jobs)
    {
        if (IsRunning)
            return false;

        ResetForNewRun();

        foreach (var job in jobs)
            _pendingJobs.Enqueue(job);

        Start();
        return true;
    }

    public void Start()
    {
        if (State != OrchestratorState.Idle)
            return;

        if (_pendingJobs.Count == 0)
        {
            State = OrchestratorState.Completed;
            return;
        }

        if (_artisan.IsAvailable() && (_artisan.IsListRunning() || _artisan.GetEnduranceStatus()))
        {
            _artisan.SetStopRequest(true);
            TransitionTo(OrchestratorState.WaitingForArtisanPause);
        }
        else
        {
            TransitionTo(OrchestratorState.RunningJobs);
        }
    }

    public void Update()
    {
        switch (State)
        {
            case OrchestratorState.WaitingForArtisanPause:
                UpdateWaitingForArtisanPause();
                break;

            case OrchestratorState.WaitingForArtisanIdle:
                UpdateWaitingForArtisanIdle();
                break;

            case OrchestratorState.RunningJobs:
                UpdateRunningJobs();
                break;
        }
    }

    private void UpdateWaitingForArtisanPause()
    {
        var status = _artisan.GetPauseStatus();
        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForAcknowledgement,
            status,
            DateTime.UtcNow - _stateEnteredAt,
            PauseAcknowledgementTimeout,
            ArtisanIdleTimeout);

        switch (decision.Kind)
        {
            case ArtisanPauseDecisionKind.ContinueWaiting:
                return;

            case ArtisanPauseDecisionKind.MoveToIdleWait:
                TransitionTo(OrchestratorState.WaitingForArtisanIdle);
                return;

            case ArtisanPauseDecisionKind.Fail:
                Fail($"Failed to pause Artisan ({ArtisanPauseGate.FormatStatus(status)})");
                return;
        }
    }

    private void UpdateWaitingForArtisanIdle()
    {
        var artisanStatus = _artisan.GetPauseStatus();
        var localStatus = LocalPlayerActionGate.GetStatus();

        if (ArtisanPauseGate.HasPauseAcknowledgement(artisanStatus)
            && LocalPlayerActionGate.IsReadyForAutomation(localStatus))
        {
            if (_localActionReadyAt is null)
            {
                _localActionReadyAt = DateTime.UtcNow;
                return;
            }

            if ((DateTime.UtcNow - _localActionReadyAt.Value) >= LocalActionReadyStableDuration)
            {
                TransitionTo(OrchestratorState.RunningJobs);
                return;
            }
        }
        else
        {
            _localActionReadyAt = null;
        }

        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForIdle,
            artisanStatus,
            DateTime.UtcNow - _stateEnteredAt,
            PauseAcknowledgementTimeout,
            ArtisanIdleTimeout);

        if (decision.Kind == ArtisanPauseDecisionKind.Fail)
            Fail($"Artisan did not become idle ({ArtisanPauseGate.FormatStatus(artisanStatus)}) ({LocalPlayerActionGate.FormatStatus(localStatus)})");
    }

    private void UpdateRunningJobs()
    {
        if (CurrentJob == null)
        {
            if (_pendingJobs.Count == 0)
            {
                CompleteRun();
                return;
            }

            CurrentJob = _pendingJobs.Dequeue();
            if (!CurrentJob.CanStart())
            {
                CurrentJob = null;
                return;
            }

            CurrentJob.Start(_context);
        }

        CurrentJob.Update();

        switch (CurrentJob.Status)
        {
            case JobStatus.Completed:
                CurrentJob = null;
                break;

            case JobStatus.Failed:
                var error = CurrentJob.Id;
                CurrentJob.Stop();
                CurrentJob = null;
                Fail(error);
                break;
        }
    }

    public void Abort()
    {
        Svc.Log.Info("[Starloom] Orchestrator abort requested.");
        CurrentJob?.Stop();
        CurrentJob = null;
        _pendingJobs.Clear();
        RestoreArtisanStopRequest();
        TransitionTo(OrchestratorState.Idle);
    }

    private void ResetForNewRun()
    {
        CurrentJob = null;
        _pendingJobs.Clear();
        TransitionTo(OrchestratorState.Idle);
    }

    private void CompleteRun()
    {
        RestoreArtisanStopRequest();
        TransitionTo(OrchestratorState.Completed);
        Svc.Log.Info("[Starloom] Orchestrator completed.");
    }

    private void Fail(string message)
    {
        _pendingJobs.Clear();
        RestoreArtisanStopRequest();
        TransitionTo(OrchestratorState.Failed);
        Svc.Log.Error($"[Starloom] Orchestrator failed: {message}");
    }

    private void RestoreArtisanStopRequest()
    {
        if (_artisan.IsAvailable() && _artisan.GetStopRequest())
            _artisan.SetStopRequest(false);
    }

    private void TransitionTo(OrchestratorState newState)
    {
        State = newState;
        _stateEnteredAt = DateTime.UtcNow;

        Svc.Log.Info($"[Starloom] Orchestrator -> {newState}");

        if (newState != OrchestratorState.WaitingForArtisanIdle)
            _localActionReadyAt = null;
    }

    public void Dispose()
    {
        if (State is not OrchestratorState.Idle and not OrchestratorState.Completed and not OrchestratorState.Failed)
            Abort();
    }
}
