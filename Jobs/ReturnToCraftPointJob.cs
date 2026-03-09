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
    public JobStatus Status { get; private set; } = JobStatus.Idle;

    public bool CanStart()
        => true;

    public void Start(JobContext context)
    {
        _context = context;
        ResetRunState();

        var configuredPoint = context.Config.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        if (!HousingReturnPointService.TryResolveConfiguredPoint(configuredPoint, out var resolvedPoint))
        {
            Fail("Return point is no longer valid. Please choose it again.");
            return;
        }

        _target = resolvedPoint;
        Status = JobStatus.Running;
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
                    break;
            }
        }
        catch (Exception ex)
        {
            Fail($"Return-to-craft-point failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        _context?.Navigation.Stop();
        ResetRunState();
        Status = JobStatus.Idle;
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
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        if (_target!.IsInn)
        {
            if (!LifestreamIPC.IsAvailable())
            {
                Fail("Lifestream is not available, so the inn shortcut cannot be used.");
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
            Fail("Could not teleport to the return point.");
            return;
        }

        _lastAction = DateTime.UtcNow;
        _observedTransition = false;
        TransitionTo(ReturnState.WaitingForTeleport);
    }

    private void WaitForTeleport()
    {
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
            Fail("Timed out while waiting for residential teleport.");
    }

    private void WaitForInn()
    {

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
            Fail("Timed out while waiting for inn teleport.");
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
            if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(15))
                Fail("House entrance not found.");
            return;
        }

        if (!_navigationStarted)
        {
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
            Fail(_context.Navigation.ErrorMessage ?? "Could not reach the house entrance.");
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

        if (_context!.NpcInteraction.TryInteract(entrance, 4f))
        {
            _lastAction = DateTime.UtcNow;
            TransitionTo(ReturnState.ConfirmingEntry);
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(10))
            Fail("Could not interact with the house entrance.");
    }

    private void ConfirmEntry()
    {
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }


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
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > TimeSpan.FromSeconds(20))
            Fail("Timed out while waiting to enter the house.");
    }

    private void Complete()
    {
        _context?.Navigation.Stop();
        Status = JobStatus.Completed;
        _state = ReturnState.Idle;
    }

    private void Fail(string message)
    {
        _context?.Navigation.Stop();
        Status = JobStatus.Failed;
        Svc.Log.Error($"[Starloom] ReturnToCraftPointJob failed: {message}");
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
