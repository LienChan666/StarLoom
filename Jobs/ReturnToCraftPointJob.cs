using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using StarLoom.Core;
using StarLoom.Data;
using StarLoom.IPC;
using StarLoom.Services;
using StarLoom.Services.Interfaces;
using System;
using static ECommons.GenericHelpers;

namespace StarLoom.Jobs;

public sealed unsafe class ReturnToCraftPointJob : AutomationJobBase
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

    private HousingReturnPoint? _target;
    private ReturnState _state = ReturnState.Idle;
    private bool _navigationStarted;
    private bool _observedTransition;

    public override string Id => "return-to-craft-point";

    public override bool CanStart()
        => true;

    public override void Start(JobContext context)
    {
        base.Start(context);
        ResetRunState();

        var configuredPoint = context.Config.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        if (!HousingReturnPointService.TryResolveConfiguredPoint(configuredPoint, out var resolvedPoint))
        {
            Fail("Return point is no longer valid. Please choose it again.");
            return;
        }

        _target = resolvedPoint;
        TransitionTo(ReturnState.Teleporting);
    }

    public override void Update()
    {
        if (Status != JobStatus.Running || Context == null || _target == null)
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

    public override void Stop()
    {
        ResetRunState();
        base.Stop();
    }

    private void ResetRunState()
    {
        _target = null;
        _state = ReturnState.Idle;
        LastActionAt = DateTime.MinValue;
        TransitionedAt = DateTime.MinValue;
        _navigationStarted = false;
        _observedTransition = false;
    }

    private void TeleportToReturnPoint()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        if (_target!.IsInn)
        {
            if (!LifestreamIPC.IsAvailable())
            {
                Fail("Lifestream is not available, so the inn shortcut cannot be used.");
                return;
            }

            LifestreamIPC.EnqueueInnShortcut();
            LastActionAt = DateTime.UtcNow;
            _observedTransition = false;
            TransitionTo(ReturnState.WaitingForInn);
            return;
        }

        if (!HousingReturnPointService.TeleportTo(_target))
        {
            Fail("Could not teleport to the return point.");
            return;
        }

        LastActionAt = DateTime.UtcNow;
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
            && (_observedTransition || (DateTime.UtcNow - LastActionAt) > TimeSpan.FromSeconds(2)))
        {
            TransitionTo(ReturnState.MovingToEntrance);
            return;
        }

        if ((DateTime.UtcNow - TransitionedAt) > TimeSpan.FromSeconds(15))
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

        if ((DateTime.UtcNow - TransitionedAt) > _innTeleportTimeout)
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
            if ((DateTime.UtcNow - TransitionedAt) > TimeSpan.FromSeconds(15))
                Fail("House entrance not found.");
            return;
        }

        if (!_navigationStarted)
        {
            Context!.Navigation.NavigateTo(new NavigationTarget(
                entrance.Position,
                0,
                Svc.ClientState.TerritoryType,
                3f));
            _navigationStarted = true;
            return;
        }

        if (Context!.Navigation.State == NavigationStatus.Arrived)
        {
            Context.Navigation.Stop();
            _navigationStarted = false;
            TransitionTo(ReturnState.InteractingEntrance);
            return;
        }

        if (Context.Navigation.State == NavigationStatus.Failed)
            Fail(Context.Navigation.ErrorMessage ?? "Could not reach the house entrance.");
    }

    private void InteractEntrance()
    {
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }

        if ((DateTime.UtcNow - LastActionAt) < TimeSpan.FromSeconds(1))
            return;

        if (Svc.Objects.LocalPlayer is not { } localPlayer)
            return;

        if (!HousingReturnPointService.TryGetHousingEntrance(localPlayer.Position, _target!.IsApartment, out var entrance) || entrance == null)
        {
            TransitionTo(ReturnState.MovingToEntrance);
            return;
        }

        if (Context!.NpcInteraction.TryInteract(entrance, 4f))
        {
            LastActionAt = DateTime.UtcNow;
            TransitionTo(ReturnState.ConfirmingEntry);
            return;
        }

        if ((DateTime.UtcNow - TransitionedAt) > TimeSpan.FromSeconds(10))
            Fail("Could not interact with the house entrance.");
    }

    private void ConfirmEntry()
    {
        if (HousingReturnPointService.IsInsideHouse())
        {
            TransitionTo(ReturnState.Completed);
            return;
        }


        if (_target!.IsApartment)
        {
            if (TryGetAddonByName<AddonSelectString>("SelectString", out var selectStringAddon)
                && IsAddonReady(&selectStringAddon->AtkUnitBase))
            {
                var selectString = new AddonMaster.SelectString((nint)selectStringAddon);
                if (selectString.EntryCount > 0)
                {
                    selectString.Entries[0].Select();
                    LastActionAt = DateTime.UtcNow;
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
                LastActionAt = DateTime.UtcNow;
                TransitionTo(ReturnState.WaitingForIndoor);
                return;
            }
        }

        if ((DateTime.UtcNow - TransitionedAt) > TimeSpan.FromSeconds(6))
        {
            LastActionAt = DateTime.MinValue;
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

        if ((DateTime.UtcNow - TransitionedAt) > TimeSpan.FromSeconds(20))
            Fail("Timed out while waiting to enter the house.");
    }

    private void Complete()
    {
        MarkCompleted();
        _state = ReturnState.Idle;
    }

    private void Fail(string message)
    {
        FailJob(message);
        _state = ReturnState.Failed;
    }

    private void TransitionTo(ReturnState state)
    {
        _state = state;
        TransitionedAt = DateTime.UtcNow;
    }

    private static bool IsTransitioning()
        => Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51];
}
