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
            return;
        }

        if (!ManagedSession.TryStart())
            return;
    }

    public void StartCollectableTurnIn()
        => StartJobs(new IAutomationJob[] { new CollectableTurnInJob() }, "收藏品提交");

    public void StartPurchaseOnly()
    {
        if (!WorkflowValidator.CanStartPurchaseWorkflow(out var purchaseError))
        {
            return;
        }

        StartJobs(WorkflowBuilder.CreatePurchaseWorkflow(), "工票购买");
    }

    public void StopAutomation()
    {
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
            return;
        }

        if (ManagedSession.IsActive)
        {
            return;
        }

        if (!Orchestrator.TryStart(jobs))
            return;
    }
}
