using Dalamud.Game.ClientState.Conditions;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using Starloom.Data;
using Starloom.GameInterop.IPC;
using Starloom.Services;
using System;
using static ECommons.GenericHelpers;

namespace Starloom.Tasks;

internal static unsafe class TaskReturnToCraftPoint
{
    private static readonly TimeSpan innTeleportTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan actionDelay = TimeSpan.FromMilliseconds(500);

    private static HousingReturnPoint? target;
    private static bool navigationStarted;
    private static bool observedTransition;
    private static DateTime lastActionAt = DateTime.MinValue;
    private static DateTime transitionedAt = DateTime.MinValue;

    internal static void Enqueue()
    {
        ResetState();
        P.TM.Enqueue(ResolveTarget, "Return.Resolve");
        P.TM.Enqueue(TeleportToReturnPoint, "Return.Teleport");
        P.TM.Enqueue(WaitForTeleport, "Return.WaitTeleport");
        P.TM.Enqueue(WaitForInn, "Return.WaitInn");
        P.TM.Enqueue(MoveToEntrance, "Return.MoveToEntrance");
        P.TM.Enqueue(InteractEntrance, "Return.Interact");
        P.TM.Enqueue(ConfirmEntry, "Return.Confirm");
        P.TM.Enqueue(WaitForIndoor, "Return.WaitIndoor");
    }

    private static void ResetState()
    {
        target = null;
        navigationStarted = false;
        observedTransition = false;
        lastActionAt = DateTime.MinValue;
        transitionedAt = DateTime.MinValue;
    }

    private static bool? ResolveTarget()
    {
        var configuredPoint = C.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        if (!HousingReturnPointService.TryResolveConfiguredPoint(configuredPoint, out var resolvedPoint))
        {
            DuoLog.Error("Return point is no longer valid. Please choose it again.");
            return null;
        }

        target = resolvedPoint;
        transitionedAt = DateTime.UtcNow;
        return true;
    }

    private static bool? TeleportToReturnPoint()
    {
        if (target == null) return null;
        if ((DateTime.UtcNow - lastActionAt) < actionDelay) return false;

        if (target.IsInn)
        {
            if (!LifestreamIpc.IsAvailable())
            {
                DuoLog.Error("Lifestream is not available, so the inn shortcut cannot be used.");
                return null;
            }

            LifestreamIpc.EnqueueInnShortcut();
            lastActionAt = DateTime.UtcNow;
            observedTransition = false;
            transitionedAt = DateTime.UtcNow;
            return true; // Next: WaitForInn
        }

        if (!HousingReturnPointService.TeleportTo(target))
        {
            DuoLog.Error("Could not teleport to the return point.");
            return null;
        }

        lastActionAt = DateTime.UtcNow;
        observedTransition = false;
        transitionedAt = DateTime.UtcNow;
        return true; // Next: WaitForTeleport
    }

    private static bool? WaitForTeleport()
    {
        if (target == null) return null;
        if (target.IsInn) return true; // Skip - we went to inn, WaitForInn will handle

        if (IsTransitioning())
        {
            observedTransition = true;
            return false;
        }

        if (Svc.ClientState.TerritoryType == target.TerritoryId
            && (observedTransition || (DateTime.UtcNow - lastActionAt) > TimeSpan.FromSeconds(2)))
        {
            return true; // Next: MoveToEntrance
        }

        if ((DateTime.UtcNow - transitionedAt) > TimeSpan.FromSeconds(15))
        {
            DuoLog.Error("Timed out while waiting for residential teleport.");
            return null;
        }

        return false;
    }

    private static bool? WaitForInn()
    {
        if (target == null) return null;
        if (!target.IsInn) return true; // Skip - we teleported to housing, already in MoveToEntrance

        if (HousingReturnPointService.IsInsideInn())
            return true; // Done

        if (IsTransitioning() || LifestreamIpc.IsBusy())
        {
            observedTransition = true;
            return false;
        }

        if ((DateTime.UtcNow - transitionedAt) > innTeleportTimeout)
        {
            DuoLog.Error("Timed out while waiting for inn teleport.");
            return null;
        }

        return false;
    }

    private static bool? MoveToEntrance()
    {
        if (target == null) return null;
        if (target.IsInn) return true; // Skip - inn teleport lands us inside
        if (HousingReturnPointService.IsInsideHouse()) return true;

        if (Svc.Objects.LocalPlayer is not { } localPlayer)
            return false;

        if (!HousingReturnPointService.TryGetHousingEntrance(localPlayer.Position, target.IsApartment, out var entrance) || entrance == null)
        {
            if ((DateTime.UtcNow - transitionedAt) > TimeSpan.FromSeconds(15))
            {
                DuoLog.Error("House entrance not found.");
                return null;
            }
            return false;
        }

        if (!navigationStarted)
        {
            P.Navigation.NavigateTo(new NavigationTarget(
                entrance.Position,
                0,
                Svc.ClientState.TerritoryType,
                3f));
            navigationStarted = true;
            transitionedAt = DateTime.UtcNow;
            return false;
        }

        if (P.Navigation.State == NavigationStatus.Arrived)
        {
            P.Navigation.Stop();
            navigationStarted = false;
            transitionedAt = DateTime.UtcNow;
            return true;
        }

        if (P.Navigation.State == NavigationStatus.Failed)
        {
            DuoLog.Error(P.Navigation.ErrorMessage ?? "Could not reach the house entrance.");
            return null;
        }

        return false;
    }

    private static bool? InteractEntrance()
    {
        if (target == null) return null;
        if (HousingReturnPointService.IsInsideHouse()) return true;

        if ((DateTime.UtcNow - lastActionAt) < TimeSpan.FromSeconds(1))
            return false;

        if (Svc.Objects.LocalPlayer is not { } localPlayer)
            return false;

        if (!HousingReturnPointService.TryGetHousingEntrance(localPlayer.Position, target.IsApartment, out var entrance) || entrance == null)
        {
            P.TM.EnqueueImmediate(MoveToEntrance, "Return.MoveToEntrance");
            P.TM.EnqueueImmediate(InteractEntrance, "Return.Interact");
            return true;
        }

        if (P.NpcInteraction.TryInteract(entrance, 4f))
        {
            lastActionAt = DateTime.UtcNow;
            transitionedAt = DateTime.UtcNow;
            return true;
        }

        if ((DateTime.UtcNow - transitionedAt) > TimeSpan.FromSeconds(10))
        {
            DuoLog.Error("Could not interact with the house entrance.");
            return null;
        }

        return false;
    }

    private static bool? ConfirmEntry()
    {
        if (target == null) return null;
        if (HousingReturnPointService.IsInsideHouse()) return true;

        if (target.IsApartment)
        {
            if (TryGetAddonByName<AddonSelectString>("SelectString", out var selectStringAddon)
                && IsAddonReady(&selectStringAddon->AtkUnitBase))
            {
                var selectString = new AddonMaster.SelectString((nint)selectStringAddon);
                if (selectString.EntryCount > 0)
                {
                    selectString.Entries[0].Select();
                    lastActionAt = DateTime.UtcNow;
                    transitionedAt = DateTime.UtcNow;
                    return true;
                }
            }
        }
        else
        {
            if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var yesnoAddon)
                && IsAddonReady(&yesnoAddon->AtkUnitBase))
            {
                new AddonMaster.SelectYesno((nint)yesnoAddon).Yes();
                lastActionAt = DateTime.UtcNow;
                transitionedAt = DateTime.UtcNow;
                return true;
            }
        }

        if ((DateTime.UtcNow - transitionedAt) > TimeSpan.FromSeconds(6))
        {
            lastActionAt = DateTime.MinValue;
            P.TM.EnqueueImmediate(InteractEntrance, "Return.Interact");
            P.TM.EnqueueImmediate(ConfirmEntry, "Return.Confirm");
            return true;
        }

        return false;
    }

    private static bool? WaitForIndoor()
    {
        if (target == null) return true;
        if (HousingReturnPointService.IsInsideHouse()) return true;

        if ((DateTime.UtcNow - transitionedAt) > TimeSpan.FromSeconds(20))
        {
            DuoLog.Error("Timed out while waiting to enter the house.");
            return null;
        }

        return false;
    }

    private static bool IsTransitioning()
        => Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51];
}
