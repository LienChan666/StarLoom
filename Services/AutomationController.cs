using ECommons.DalamudServices;
using Starloom.Core;
using Starloom.Jobs;
using Starloom.Services.Interfaces;
using Starloom.Workflows;
using System.Collections.Generic;

namespace Starloom.Services;

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
            Svc.Log.Warning($"[Starloom] {errorMessage}");
            return;
        }

        if (!ManagedSession.TryStart())
            Svc.Log.Warning($"[Starloom] {ManagedSession.ErrorMessage ?? "无法启动联动流程。"}");
    }

    public void StartCollectableTurnIn()
        => StartJobs(new IAutomationJob[] { new CollectableTurnInJob() }, "收藏品提交");

    public void StartPurchaseOnly()
    {
        if (!WorkflowValidator.CanStartPurchaseWorkflow(out var purchaseError))
        {
            Svc.Log.Warning($"[Starloom] {purchaseError}");
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
            Svc.Log.Warning($"[Starloom] {errorMessage}");
            return;
        }

        if (ManagedSession.IsActive)
        {
            Svc.Log.Warning($"[Starloom] 当前正在进行 Artisan 联动，忽略 {actionName} 启动请求。");
            return;
        }

        if (!Orchestrator.TryStart(jobs))
            Svc.Log.Warning($"[Starloom] 当前已有任务在运行中，忽略 {actionName} 启动请求。");
    }
}