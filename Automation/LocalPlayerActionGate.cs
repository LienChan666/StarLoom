using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace Starloom.Automation;

public readonly record struct LocalPlayerActionStatus(
    bool HasLocalPlayer,
    bool BetweenAreas,
    bool BetweenAreas51,
    bool Crafting,
    bool PreparingToCraft,
    bool ExecutingCraftingAction,
    bool Occupied,
    bool Occupied30,
    bool OccupiedInEvent,
    bool OccupiedInQuestEvent,
    bool Occupied33,
    bool OccupiedInCutSceneEvent,
    bool OccupiedSummoningBell,
    bool Casting,
    bool TradeOpen);

public static class LocalPlayerActionGate
{
    public static LocalPlayerActionStatus GetStatus()
        => new(
            Svc.PlayerState.IsLoaded,
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

    public static bool IsReadyForAutomation(LocalPlayerActionStatus status)
        => status.HasLocalPlayer
           && !status.BetweenAreas
           && !status.BetweenAreas51
           && !status.Crafting
           && !status.PreparingToCraft
           && !status.ExecutingCraftingAction
           && !status.Occupied
           && !status.Occupied30
           && !status.OccupiedInEvent
           && !status.OccupiedInQuestEvent
           && !status.Occupied33
           && !status.OccupiedInCutSceneEvent
           && !status.OccupiedSummoningBell
           && !status.Casting
           && !status.TradeOpen;

    public static string FormatStatus(LocalPlayerActionStatus status)
        => $"localPlayer={status.HasLocalPlayer}, betweenAreas={status.BetweenAreas}, betweenAreas51={status.BetweenAreas51}, crafting={status.Crafting}, preparingToCraft={status.PreparingToCraft}, executingCraftingAction={status.ExecutingCraftingAction}, occupied={status.Occupied}, occupied30={status.Occupied30}, occupiedInEvent={status.OccupiedInEvent}, occupiedInQuestEvent={status.OccupiedInQuestEvent}, occupied33={status.Occupied33}, occupiedInCutSceneEvent={status.OccupiedInCutSceneEvent}, occupiedSummoningBell={status.OccupiedSummoningBell}, casting={status.Casting}, tradeOpen={status.TradeOpen}";
}
