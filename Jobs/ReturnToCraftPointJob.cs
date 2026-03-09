using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using StarLoom.Core;
using StarLoom.Data;
using StarLoom.IPC;
using StarLoom.Services;
using System;
using static ECommons.GenericHelpers;

namespace StarLoom.Jobs;

public sealed unsafe class ReturnToCraftPointJob : IAutomationJob
{
    private readonly TimeSpan _innTeleportTimeout = TimeSpan.FromMinutes(5);

    private enum ReturnState
    {
        Idle,
        Teleporting,
        WaitingForTeleport,
        WaitingForInn,
        MovingToEntrance,
        InteractingEntrance,
        ConfirmingEntry,
        WaitingForIndoor,
        Completed,
        Failed,
    }

    private readonly TimeSpan _actionDelay = TimeSpan.FromMilliseconds(500);

    private JobContext? _context;
    private HousingReturnPoint? _target;
    private ReturnState _state = ReturnState.Idle;
    private DateTime _lastAction = DateTime.MinValue;
    private DateTime _stateEnteredAt = DateTime.MinValue;
    private bool _navigationStarted;
    private bool _observedTransition;

    public string Id => "return-to-craft-point";
    public string StatusText { get; private set; } = "空闲";
    public JobStatus Status { get; private set; } = JobStatus.Idle;

    public bool CanStart()
        => !(Plugin.P.Config.FreeSlotThreshold > 0
            && InventoryService.GetFreeSlotCount() < Plugin.P.Config.FreeSlotThreshold
            && InventoryService.HasCollectableTurnIns());

    public void Start(JobContext context)
    {
        _context = context;
        ResetRunState();

        var configuredPoint = context.Config.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        if (!HousingReturnPointService.TryResolveConfiguredPoint(configuredPoint, out var resolvedPoint))
        {
            Fail("返回点已失效，请重新选择。");
            return;
        }

        _target = resolvedPoint;
        Status = JobStatus.Running;
        StatusText = $"正在返回 {_target.DisplayName}";
        TransitionTo(ReturnState.Teleporting);
    }

    public void Update()
    {
        if (Status != JobStatus.Running || _context == null || _target == null)
            return;

        try
        {
            switch (_state)
            {
                case ReturnState.Teleporting:
                    TeleportToReturnPoint();
                    break;
                case ReturnState.WaitingForTeleport:
                    WaitForTeleport();
                    break;
                case ReturnState.WaitingForInn:
                    WaitForInn();
                    break;
                case ReturnState.MovingToEntrance:
                    MoveToEntrance();
                    break;
                case ReturnState.InteractingEntrance:
                    InteractEntrance();
                    break;
                case ReturnState.ConfirmingEntry:
                    ConfirmEntry();
                    break;
                case ReturnState.WaitingForIndoor:
                    WaitForIndoor();
                    break;
                case ReturnState.Completed:
                    Complete();
                    break;
                case ReturnState.Failed:
                    Fail(StatusText);
                    break;
            }
        }
        catch (Exception ex)
        {
            Fail($"返回制作点异常：{ex.Message}");
        }
    }

    public void Stop()
    {
        _context?.Navigation.Stop();
        ResetRunState();
        Status = JobStatus.Idle;
        StatusText = "已停止";
    }

    private void ResetRunState()
    {
        _target = null;
        _state = ReturnState.Idle;
        _lastAction = DateTime.MinValue;
        _stateEnteredAt = DateTime.MinValue;
        _navigationStarted = false;
        _observedTransition = false;
    }

    private void TeleportToReturnPoint()
    {
        StatusText = $"正在传送到 {_target!.DisplayName}";
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        if (_target!.IsInn)
        {
            if (!LifestreamIPC.IsAvailable())
            {
                Fail("Lifestream 未就绪，无法返回旅馆。");
                return;
            }

            LifestreamIPC.EnqueueInnShortcut();
            _lastAction = DateTime.UtcNow;
            _observedTransition = false;
            TransitionTo(ReturnState.WaitingForInn);
            return;
        }

        if (!HousingReturnPointService.TeleportTo(_target))
        {
            Fail("无法传送到返回点。");
            return;
        }

        _lastAction = DateTime.UtcNow;
        _observedTransition = false;
        TransitionTo(ReturnState.WaitingForTeleport);
    }

    private void WaitForTeleport()
    {
        StatusText = "正在等待住宅传送完成";
        if (IsTransitioning())
        {
            _observedTransition = true;
            return;
        }

        if (Svc.ClientState.TerritoryType == _target!.TerritoryId
            && (_observedTransition || (DateTime.UtcNow - _lastAction) > TimeSpan.FromSeconds(2)))
        {
            TransitionTo(ReturnState.MovingToEntrance);
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(15))
            Fail("等待住宅传送超时。");
    }

    private void WaitForInn()
    {
        StatusText = "正在等待传送到旅馆";

        if (HousingReturnPointService.IsInsideInn())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }

        if (IsTransitioning() || LifestreamIPC.IsBusy())
        {
            _observedTransition = true;
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > _innTeleportTimeout)
            Fail("等待传送到旅馆超时。");
    }

    private void MoveToEntrance()
    {
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }

        if (Svc.Objects.LocalPlayer is not { } localPlayer)
            return;

        if (!HousingReturnPointService.TryGetHousingEntrance(localPlayer.Position, _target!.IsApartment, out var entrance) || entrance == null)
        {
            StatusText = "正在查找房屋入口";
            if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(15))
                Fail("未找到房屋入口。");
            return;
        }

        if (!_navigationStarted)
        {
            StatusText = "正在前往房屋入口";
            _context!.Navigation.NavigateTo(new NavigationTarget(
                entrance.Position,
                0,
                Svc.ClientState.TerritoryType,
                3f));
            _navigationStarted = true;
            return;
        }

        if (_context!.Navigation.State == NavigationService.NavigationState.Arrived)
        {
            _context.Navigation.Stop();
            _navigationStarted = false;
            TransitionTo(ReturnState.InteractingEntrance);
            return;
        }

        if (_context.Navigation.State == NavigationService.NavigationState.Failed)
            Fail(_context.Navigation.ErrorMessage ?? "无法到达房屋入口。");
    }

    private void InteractEntrance()
    {
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }

        if ((DateTime.UtcNow - _lastAction) < TimeSpan.FromSeconds(1))
            return;

        if (Svc.Objects.LocalPlayer is not { } localPlayer)
            return;

        if (!HousingReturnPointService.TryGetHousingEntrance(localPlayer.Position, _target!.IsApartment, out var entrance) || entrance == null)
        {
            TransitionTo(ReturnState.MovingToEntrance);
            return;
        }

        StatusText = "正在交互房屋入口";
        if (_context!.NpcInteraction.TryInteract(entrance, 4f))
        {
            _lastAction = DateTime.UtcNow;
            TransitionTo(ReturnState.ConfirmingEntry);
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(10))
            Fail("无法与房屋入口交互。");
    }

    private void ConfirmEntry()
    {
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }

        StatusText = _target!.IsApartment ? "正在选择进入房间" : "正在确认进入房屋";

        if (_target.IsApartment)
        {
            if (TryGetAddonByName<AddonSelectString>("SelectString", out var selectStringAddon)
                && IsAddonReady(&selectStringAddon->AtkUnitBase))
            {
                var selectString = new AddonMaster.SelectString((nint)selectStringAddon);
                if (selectString.EntryCount > 0)
                {
                    selectString.Entries[0].Select();
                    _lastAction = DateTime.UtcNow;
                    TransitionTo(ReturnState.WaitingForIndoor);
                    return;
                }
            }
        }
        else
        {
            if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var yesnoAddon)
                && IsAddonReady(&yesnoAddon->AtkUnitBase))
            {
                new AddonMaster.SelectYesno((nint)yesnoAddon).Yes();
                _lastAction = DateTime.UtcNow;
                TransitionTo(ReturnState.WaitingForIndoor);
                return;
            }
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(6))
        {
            _lastAction = DateTime.MinValue;
            TransitionTo(ReturnState.InteractingEntrance);
        }
    }

    private void WaitForIndoor()
    {
        StatusText = "正在等待进入房屋";
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(20))
            Fail("等待进入房屋超时。");
    }

    private void Complete()
    {
        _context?.Navigation.Stop();
        Status = JobStatus.Completed;
        StatusText = _target?.IsInn == true
            ? "已返回旅馆"
            : "已返回制作点并进入房屋";
        _state = ReturnState.Idle;
    }

    private void Fail(string message)
    {
        _context?.Navigation.Stop();
        Status = JobStatus.Failed;
        StatusText = message;
        _state = ReturnState.Failed;
    }

    private void TransitionTo(ReturnState state)
    {
        _state = state;
        _stateEnteredAt = DateTime.UtcNow;
    }

    private static bool IsTransitioning()
        => Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51];
}
