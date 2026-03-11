using Dalamud.Game.ClientState.Conditions;
using StarLoom.Tasks.Artisan;

namespace StarLoom.Game;

public sealed class PlayerStateGame
{
    public bool IsPlayerAvailable()
    {
        return Svc.ClientState.IsLoggedIn && Svc.PlayerState.IsLoaded;
    }

    public LocalPlayerActionStatus GetLocalPlayerActionStatus()
    {
        return new LocalPlayerActionStatus(
            IsPlayerAvailable(),
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
