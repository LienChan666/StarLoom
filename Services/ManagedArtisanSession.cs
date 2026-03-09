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
    public string StatusText { get; private set; } = "空闲";
    public string? ErrorMessage { get; private set; }
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

    public string GetStateText()
        => State switch
        {
            ManagedArtisanSessionState.Idle => "空闲",
            ManagedArtisanSessionState.WaitingForPreStartJobs => "启动前预处理",
            ManagedArtisanSessionState.WaitingForArtisanStart => "等待 Artisan 启动",
            ManagedArtisanSessionState.Monitoring => "监控 Artisan",
            ManagedArtisanSessionState.WaitingForThresholdJobs => "低空格处理中",
            ManagedArtisanSessionState.Failed => "联动失败",
            _ => State.ToString(),
        };

    public bool TryStart()
    {
        if (IsActive || _orchestrator.IsRunning)
        {
            SetFailure("当前已有联动流程在运行中。", stopArtisan: false);
            return false;
        }

        if (!_artisan.IsAvailable())
        {
            SetFailure("未检测到 Artisan，无法启动联动。", stopArtisan: false);
            return false;
        }

        if (_config.ArtisanListId <= 0)
        {
            SetFailure("请先在设置中填写 Artisan 清单 ID。", stopArtisan: false);
            return false;
        }

        if (_artisan.IsListRunning() || _artisan.GetEnduranceStatus() || _artisan.IsBusy())
        {
            SetFailure("Artisan 当前已有任务运行，请先停止后再由 Starloom 接管。", stopArtisan: false);
            return false;
        }

        ErrorMessage = null;
        _warnedMissingCollectablesAtThreshold = false;

        if (_artisan.GetStopRequest())
            _artisan.SetStopRequest(false);

        if (!IsBelowFreeSlotThreshold())
            return TryStartArtisanList("正在启动 Artisan 清单...");

        if (!InventoryService.HasCollectableTurnIns())
        {
            SetFailure("当前背包空格已低于阈值，且没有可提交的收藏品，无法启动 Artisan 清单。", stopArtisan: false);
            return false;
        }

        if (!_orchestrator.TryStart(_jobFactory()))
        {
            SetFailure("无法启动 Starloom 预处理流程。", stopArtisan: false);
            return false;
        }

        TransitionTo(ManagedArtisanSessionState.WaitingForPreStartJobs, "背包空格不足，正在先执行收藏品流程...", preserveError: false);
        return true;
    }

    public void Stop()
    {
        if (_orchestrator.IsRunning)
            _orchestrator.Abort();

        if (_artisan.IsAvailable() && (_artisan.IsListRunning() || _artisan.GetEnduranceStatus() || _artisan.IsBusy()))
            _artisan.SetStopRequest(true);

        ErrorMessage = null;
        _warnedMissingCollectablesAtThreshold = false;
        TransitionTo(ManagedArtisanSessionState.Idle, "已停止联动流程。", preserveError: false);
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
        {
            StatusText = "正在执行启动前的收藏品流程...";
            return;
        }

        if (_orchestrator.State == OrchestratorState.Failed)
        {
            SetFailure(_orchestrator.ErrorMessage ?? "启动前的收藏品流程失败。", stopArtisan: false);
            return;
        }

        if (_orchestrator.State == OrchestratorState.Completed)
            TryStartArtisanList("收藏品流程完成，正在启动 Artisan 清单...");
    }

    private void UpdateWaitingForArtisanStart()
    {
        if (_artisan.IsListRunning())
        {
            _warnedMissingCollectablesAtThreshold = false;
            TransitionTo(ManagedArtisanSessionState.Monitoring, "Artisan 清单已启动，正在监控背包空位。", preserveError: false);
            return;
        }

        if (TimedOut(8))
            SetFailure("等待 Artisan 启动清单超时。", stopArtisan: false);
    }

    private void UpdateMonitoring()
    {
        if (_orchestrator.IsRunning)
        {
            TransitionTo(ManagedArtisanSessionState.WaitingForThresholdJobs, "正在执行收藏品流程...", preserveError: false);
            return;
        }

        if (!_artisan.IsListRunning())
        {
            TransitionTo(ManagedArtisanSessionState.Idle, "Artisan 清单已结束。", preserveError: false);
            return;
        }

        if (!IsBelowFreeSlotThreshold())
        {
            _warnedMissingCollectablesAtThreshold = false;
            StatusText = "Artisan 清单运行中，正在监控背包空位。";
            return;
        }

        if (!InventoryService.HasCollectableTurnIns())
        {
            StatusText = "背包空格低于阈值，但没有可提交的收藏品。";
            if (!_warnedMissingCollectablesAtThreshold)
            {
                _warnedMissingCollectablesAtThreshold = true;
            }

            return;
        }

        if (!_orchestrator.TryStart(_jobFactory()))
        {
            SetFailure("无法启动低空格接管流程。", stopArtisan: true);
            return;
        }

        _warnedMissingCollectablesAtThreshold = false;
        TransitionTo(ManagedArtisanSessionState.WaitingForThresholdJobs, "背包空格不足，正在执行收藏品流程...", preserveError: false);
    }

    private void UpdateThresholdJobs()
    {
        if (_orchestrator.IsRunning)
        {
            StatusText = "正在执行收藏品流程...";
            return;
        }

        if (_orchestrator.State == OrchestratorState.Failed)
        {
            SetFailure(_orchestrator.ErrorMessage ?? "收藏品流程失败。", stopArtisan: true);
            return;
        }

        if (_orchestrator.State == OrchestratorState.Completed && IsBelowFreeSlotThreshold() && InventoryService.HasCollectableTurnIns())
        {
            if (!_orchestrator.TryStart(_jobFactory()))
            {
                SetFailure("无法继续执行低空格接管流程。", stopArtisan: true);
                return;
            }

            TransitionTo(ManagedArtisanSessionState.WaitingForThresholdJobs, "收藏品仍未处理完，继续执行低空格流程...", preserveError: false);
            return;
        }

        if (_orchestrator.State == OrchestratorState.Completed)
            TransitionTo(ManagedArtisanSessionState.Monitoring, "收藏品流程完成，已恢复 Artisan 清单。", preserveError: false);
    }

    private bool TryStartArtisanList(string statusText)
    {
        if (_config.ArtisanListId <= 0)
        {
            SetFailure("请先在设置中填写 Artisan 清单 ID。", stopArtisan: false);
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
        TransitionTo(ManagedArtisanSessionState.WaitingForArtisanStart, statusText, preserveError: false);
        return true;
    }

    private bool IsBelowFreeSlotThreshold()
        => _config.FreeSlotThreshold > 0 && InventoryService.GetFreeSlotCount() < _config.FreeSlotThreshold;

    private void SetFailure(string message, bool stopArtisan)
    {
        ErrorMessage = message;
        if (stopArtisan && _artisan.IsAvailable() && (_artisan.IsListRunning() || _artisan.GetEnduranceStatus() || _artisan.IsBusy()))
            _artisan.SetStopRequest(true);

        TransitionTo(ManagedArtisanSessionState.Failed, message);
    }

    private void TransitionTo(ManagedArtisanSessionState newState, string statusText, bool preserveError = true)
    {
        State = newState;
        StatusText = statusText;
        _stateEnteredAt = DateTime.UtcNow;

        if (!preserveError)
            ErrorMessage = null;
    }

    private bool TimedOut(int seconds)
        => (DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(seconds);
}
