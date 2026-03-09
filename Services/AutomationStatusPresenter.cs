using Starloom.Core;

namespace Starloom.Services;

public sealed class AutomationStatusPresenter
{
    private readonly JobOrchestrator _orchestrator;
    private readonly ManagedArtisanSession _managedSession;

    public AutomationStatusPresenter(JobOrchestrator orchestrator, ManagedArtisanSession managedSession)
    {
        _orchestrator = orchestrator;
        _managedSession = managedSession;
    }

    public string GetOrchestratorStateText()
    {
        if (_managedSession.State != ManagedArtisanSessionState.Idle)
            return _managedSession.GetStateText();

        return _orchestrator.State switch
        {
            OrchestratorState.Idle => "空闲",
            OrchestratorState.WaitingForArtisanPause => "等待 Artisan 暂停",
            OrchestratorState.RunningJobs => "执行任务中",
            OrchestratorState.Completed => "已完成",
            OrchestratorState.Failed => "已失败",
            _ => _orchestrator.State.ToString(),
        };
    }

    public string GetCurrentJobDisplayName()
    {
        var currentJob = _orchestrator.CurrentJob;
        if (currentJob == null)
            return _managedSession.State != ManagedArtisanSessionState.Idle ? "Artisan 清单联动" : "无";

        return currentJob.Id switch
        {
            "collectable-turn-in" => "收藏品提交",
            "scrip-purchase" => "工票购买",
            "return-to-craft-point" => "返回制作点",
            "close-game" => "关闭游戏",
            _ => currentJob.Id,
        };
    }

    public string GetCurrentStatusText()
    {
        if (_orchestrator.CurrentJob != null && !string.IsNullOrWhiteSpace(_orchestrator.CurrentJob.StatusText))
            return _orchestrator.CurrentJob.StatusText;

        if (_managedSession.State != ManagedArtisanSessionState.Idle && !string.IsNullOrWhiteSpace(_managedSession.StatusText))
            return _managedSession.StatusText;

        return _orchestrator.State switch
        {
            OrchestratorState.Idle => "空闲",
            OrchestratorState.WaitingForArtisanPause => "正在等待 Artisan 暂停当前制作流程。",
            OrchestratorState.WaitingForArtisanIdle => "正在等待角色脱离制作和场景切换等忙碌状态。",
            OrchestratorState.RunningJobs => "正在执行自动化任务。",
            OrchestratorState.Completed => "自动化任务已完成。",
            OrchestratorState.Failed => _orchestrator.ErrorMessage ?? "自动化任务失败。",
            _ => "空闲",
        };
    }
}