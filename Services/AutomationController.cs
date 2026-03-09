using ECommons.DalamudServices;
using StarLoom.Core;
using StarLoom.Jobs;
using StarLoom.Services.Interfaces;
using StarLoom.Workflows;
using System.Collections.Generic;

namespace StarLoom.Services;

public sealed class AutomationController
{
    private readonly Configuration _config;
    private readonly INavigationService _navigation;
    private readonly JobOrchestrator _orchestrator;
    private readonly ManagedArtisanSession _managedSession;
    private readonly WorkflowBuilder _workflowBuilder;
    private readonly WorkflowStartValidator _workflowValidator;

    public AutomationController(
        Configuration config,
        INavigationService navigation,
        JobOrchestrator orchestrator,
        ManagedArtisanSession managedSession,
        WorkflowBuilder workflowBuilder,
        WorkflowStartValidator workflowValidator)
    {
        _config = config;
        _navigation = navigation;
        _orchestrator = orchestrator;
        _managedSession = managedSession;
        _workflowBuilder = workflowBuilder;
        _workflowValidator = workflowValidator;
    }

    public bool HasConfiguredPurchases => _config.ScripShopItems is { Count: > 0 };
    public bool IsAutomationBusy => _managedSession.IsActive || _orchestrator.IsRunning;

    public void StartConfiguredWorkflow()
    {
        if (!_workflowValidator.CanStartCollectableWorkflow(out var errorMessage))
        {
            Svc.Log.Error($"[Starloom] Cannot start configured workflow: {errorMessage}");
            return;
        }

        if (!_managedSession.TryStart())
            return;

        Svc.Log.Info("[Starloom] Started configured workflow.");
    }

    public void StartCollectableTurnIn()
        => StartJobs(new IAutomationJob[] { new CollectableTurnInJob() }, "collectable-turn-in");

    public void StartPurchaseOnly()
    {
        if (!_workflowValidator.CanStartPurchaseWorkflow(out var purchaseError))
        {
            Svc.Log.Error($"[Starloom] Cannot start purchase workflow: {purchaseError}");
            return;
        }

        StartJobs(_workflowBuilder.CreatePurchaseWorkflow(), "purchase");
    }

    public void StopAutomation()
    {
        Svc.Log.Info("[Starloom] Stop requested.");
        if (_managedSession.IsActive)
        {
            _managedSession.Stop();
            return;
        }

        if (_orchestrator.IsRunning)
            _orchestrator.Abort();
    }

    public void Update()
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        _navigation.Update();
        _orchestrator.Update();
        _managedSession.Update();
    }

    private void StartJobs(IEnumerable<IAutomationJob> jobs, string actionName)
    {
        if (!_workflowValidator.CanStartCollectableWorkflow(out var errorMessage))
        {
            Svc.Log.Error($"[Starloom] Cannot start {actionName}: {errorMessage}");
            return;
        }

        if (_managedSession.IsActive)
        {
            Svc.Log.Warning($"[Starloom] Skipped {actionName}: managed session is active.");
            return;
        }

        if (!_orchestrator.TryStart(jobs))
            return;

        Svc.Log.Info($"[Starloom] Started {actionName}.");
    }
}
