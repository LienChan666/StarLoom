using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using StarLoom.Tasks.Artisan;
using StarLoom.Tasks.Navigation;
using System.Numerics;

namespace StarLoom.Game;

public sealed unsafe class LocationGame
{
    public bool IsInsideHouse()
    {
        return ReturnPointGame.IsInsideHouse();
    }

    public bool IsInsideInn()
    {
        return ReturnPointGame.IsInsideInn();
    }

    public bool IsTransitioning()
    {
        return Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51];
    }

    public uint GetCurrentTerritoryId()
    {
        return Svc.ClientState.TerritoryType;
    }

    public Vector3 GetLocalPlayerPosition()
    {
        return Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
    }

    public NavigationRuntimeSnapshot GetNavigationSnapshot()
    {
        return new NavigationRuntimeSnapshot(
            GetCurrentTerritoryId(),
            GetLocalPlayerPosition(),
            LocalPlayerActionGate.IsReadyForAutomation(BuildLocalPlayerActionStatus()));
    }

    public static bool TeleportToAetheryte(uint aetheryteId, byte subIndex = 0)
    {
        if (aetheryteId == 0)
            return false;

        var telepo = Telepo.Instance();
        if (telepo == null)
            return false;

        if (!IsAttuned(aetheryteId))
            return false;

        telepo->Teleport(aetheryteId, subIndex);
        return true;
    }

    private static bool IsAttuned(uint aetheryteId)
    {
        var telepo = Telepo.Instance();
        if (telepo == null)
            return false;

        if (!Svc.PlayerState.IsLoaded)
            return true;

        telepo->UpdateAetheryteList();
        var endPtr = telepo->TeleportList.Last;
        for (var it = telepo->TeleportList.First; it != endPtr; ++it)
        {
            if (it->AetheryteId == aetheryteId)
                return true;
        }

        return false;
    }

    private static LocalPlayerActionStatus BuildLocalPlayerActionStatus()
    {
        return new LocalPlayerActionStatus(
            Svc.ClientState.IsLoggedIn && Svc.PlayerState.IsLoaded,
            Svc.Condition[ConditionFlag.BetweenAreas],
            Svc.Condition[ConditionFlag.BetweenAreas51],
            Svc.Condition[ConditionFlag.Crafting],
            Svc.Condition[ConditionFlag.PreparingToCraft],
            Svc.Condition[ConditionFlag.ExecutingCraftingAction],
            Svc.Condition[ConditionFlag.Occupied],
            Svc.Condition[ConditionFlag.Occupied30],
            Svc.Condition[ConditionFlag.OccupiedInEvent],
            Svc.Condition[ConditionFlag.OccupiedInQuestEvent],
            Svc.Condition[ConditionFlag.Occupied33],
            Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent],
            Svc.Condition[ConditionFlag.OccupiedSummoningBell],
            Svc.Condition[ConditionFlag.Casting],
            Svc.Condition[ConditionFlag.TradeOpen]);
    }
}
