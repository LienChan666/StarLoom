using StarLoom.Core;

namespace StarLoom.Services;

public sealed class AutomationStatusPresenter
{
    private readonly JobOrchestrator _orchestrator;
    private readonly ManagedArtisanSession _managedSession;

    public AutomationStatusPresenter(JobOrchestrator orchestrator, ManagedArtisanSession managedSession)
    {
        _orchestrator = orchestrator;
        _managedSession = managedSession;
    }

    public string GetOrchestratorStateKey()
    {
        if (_managedSession.State != ManagedArtisanSessionState.Idle)
            return _managedSession.GetStateKey();

        return _orchestrator.State switch
        {
            OrchestratorState.Idle => "state.orchestrator.idle",
            OrchestratorState.WaitingForArtisanPause => "state.orchestrator.waiting_pause",
            OrchestratorState.WaitingForArtisanIdle => "state.orchestrator.waiting_idle",
            OrchestratorState.RunningJobs => "state.orchestrator.running",
            OrchestratorState.Completed => "state.orchestrator.completed",
            OrchestratorState.Failed => "state.orchestrator.failed",
            _ => "state.orchestrator.idle",
        };
    }
}
