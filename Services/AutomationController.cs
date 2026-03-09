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

    private INavigationService Navigation => _navigation;
    private JobOrchestrator Orchestrator => _orchestrator;
    private ManagedArtisanSession ManagedSession => _managedSession;
    private WorkflowBuilder WorkflowBuilder => _workflowBuilder;
    private WorkflowStartValidator WorkflowValidator => _workflowValidator;

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
    public bool HasConfiguredCollectableShop => _config.PreferredCollectableShop != null;
    public bool IsAutomationBusy => ManagedSession.IsActive || Orchestrator.IsRunning;

    public void StartConfiguredWorkflow()
    {
        if (!WorkflowValidator.CanStartCollectableWorkflow(out var errorMessage))
        {
            Svc.Log.Error($"[Starloom] Cannot start configured workflow: {errorMessage}");
            return;
        }

        if (!ManagedSession.TryStart())
            return;

        Svc.Log.Info("[Starloom] Started configured workflow.");
    }

    public void StartCollectableTurnIn()
        => StartJobs(new IAutomationJob[] { new CollectableTurnInJob() }, "collectable-turn-in");

    public void StartPurchaseOnly()
    {
        if (!WorkflowValidator.CanStartPurchaseWorkflow(out var purchaseError))
        {
            Svc.Log.Error($"[Starloom] Cannot start purchase workflow: {purchaseError}");
            return;
        }

        StartJobs(WorkflowBuilder.CreatePurchaseWorkflow(), "purchase");
    }

    public void StopAutomation()
    {
        Svc.Log.Info("[Starloom] Stop requested.");
        if (ManagedSession.IsActive)
        {
            ManagedSession.Stop();
            return;
        }

        if (Orchestrator.IsRunning)
            Orchestrator.Abort();
    }

    public void Update()
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        Navigation.Update();
        Orchestrator.Update();
        ManagedSession.Update();
    }

    private void StartJobs(IEnumerable<IAutomationJob> jobs, string actionName)
    {
        if (!WorkflowValidator.CanStartCollectableWorkflow(out var errorMessage))
        {
            Svc.Log.Error($"[Starloom] Cannot start {actionName}: {errorMessage}");
            return;
        }

        if (ManagedSession.IsActive)
        {
            Svc.Log.Warning($"[Starloom] Skipped {actionName}: managed session is active.");
            return;
        }

        if (!Orchestrator.TryStart(jobs))
            return;

        Svc.Log.Info($"[Starloom] Started {actionName}.");
    }
}
