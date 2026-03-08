using ECommons.DalamudServices;
using Starloom.IPC;
using System;
using System.Collections.Generic;

namespace Starloom.Core;

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

    private readonly ArtisanIPC _artisan;
    private readonly JobContext _context;
    private readonly Queue<IAutomationJob> _pendingJobs = new();

    private DateTime _stateEnteredAt = DateTime.MinValue;
    private DateTime? _localActionReadyAt;

    public IAutomationJob? CurrentJob { get; private set; }
    public OrchestratorState State { get; private set; } = OrchestratorState.Idle;
    public string? ErrorMessage { get; private set; }
    public bool IsRunning => State is OrchestratorState.WaitingForArtisanPause or OrchestratorState.WaitingForArtisanIdle or OrchestratorState.RunningJobs;

    public JobOrchestrator(ArtisanIPC artisan, JobContext context)
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

        ErrorMessage = null;
        if (_artisan.IsAvailable() && (_artisan.IsListRunning() || _artisan.GetEnduranceStatus()))
        {
            _artisan.SetStopRequest(true);
            Svc.Log.Information("[Orchestrator] Requested Artisan stop before running queued jobs");
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
                Svc.Log.Information($"[Orchestrator] Artisan pause acknowledged, waiting for local control ({ArtisanPauseGate.FormatStatus(status)})");
                TransitionTo(OrchestratorState.WaitingForArtisanIdle);
                return;

            case ArtisanPauseDecisionKind.Fail:
                Fail(decision.FailureMessage ?? $"等待 Artisan 暂停失败（{ArtisanPauseGate.FormatStatus(status)}）");
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
                Svc.Log.Information($"[Orchestrator] Local player control looks ready; waiting for stability ({ArtisanPauseGate.FormatStatus(artisanStatus)}; {LocalPlayerActionGate.FormatStatus(localStatus)})");
                return;
            }

            if ((DateTime.UtcNow - _localActionReadyAt.Value) >= LocalActionReadyStableDuration)
            {
                Svc.Log.Information($"[Orchestrator] Artisan pause acknowledged and local control is stable; starting Starloom jobs ({ArtisanPauseGate.FormatStatus(artisanStatus)}; {LocalPlayerActionGate.FormatStatus(localStatus)})");
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
            Fail($"{decision.FailureMessage}（{LocalPlayerActionGate.FormatStatus(localStatus)}）");
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
                Svc.Log.Warning($"[Orchestrator] Job '{CurrentJob.Id}' CanStart() returned false, skipping");
                CurrentJob = null;
                return;
            }

            CurrentJob.Start(_context);
        }

        CurrentJob.Update();

        switch (CurrentJob.Status)
        {
            case JobStatus.Completed:
                Svc.Log.Information($"[Orchestrator] Job '{CurrentJob.Id}' completed");
                CurrentJob = null;
                break;

            case JobStatus.Failed:
                var error = CurrentJob.StatusText;
                Svc.Log.Error($"[Orchestrator] Job '{CurrentJob.Id}' failed: {error}");
                CurrentJob.Stop();
                CurrentJob = null;
                Fail(error);
                break;
        }
    }

    public void Abort()
    {
        CurrentJob?.Stop();
        CurrentJob = null;
        _pendingJobs.Clear();
        ErrorMessage = null;
        RestoreArtisanStopRequest();
        TransitionTo(OrchestratorState.Idle);
    }

    private void ResetForNewRun()
    {
        CurrentJob = null;
        _pendingJobs.Clear();
        ErrorMessage = null;
        TransitionTo(OrchestratorState.Idle);
    }

    private void CompleteRun()
    {
        ErrorMessage = null;
        RestoreArtisanStopRequest();
        TransitionTo(OrchestratorState.Completed);
    }

    private void Fail(string message)
    {
        ErrorMessage = message;
        _pendingJobs.Clear();
        RestoreArtisanStopRequest();
        TransitionTo(OrchestratorState.Failed);
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

        if (newState != OrchestratorState.WaitingForArtisanIdle)
            _localActionReadyAt = null;
    }

    public void Dispose()
    {
        if (State is not OrchestratorState.Idle and not OrchestratorState.Completed and not OrchestratorState.Failed)
            Abort();
    }
}
