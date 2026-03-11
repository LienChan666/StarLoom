namespace StarLoom.Tasks.Artisan;

public readonly record struct LocalPlayerActionStatus(
    bool hasLocalPlayer,
    bool betweenAreas,
    bool betweenAreas51,
    bool crafting,
    bool preparingToCraft,
    bool executingCraftingAction,
    bool occupied,
    bool occupied30,
    bool occupiedInEvent,
    bool occupiedInQuestEvent,
    bool occupied33,
    bool occupiedInCutSceneEvent,
    bool occupiedSummoningBell,
    bool casting,
    bool tradeOpen);

public static class LocalPlayerActionGate
{
    public static bool IsReadyForAutomation(LocalPlayerActionStatus status)
    {
        return status.hasLocalPlayer
            && !status.betweenAreas
            && !status.betweenAreas51
            && !status.crafting
            && !status.preparingToCraft
            && !status.executingCraftingAction
            && !status.occupied
            && !status.occupied30
            && !status.occupiedInEvent
            && !status.occupiedInQuestEvent
            && !status.occupied33
            && !status.occupiedInCutSceneEvent
            && !status.occupiedSummoningBell
            && !status.casting
            && !status.tradeOpen;
    }
}
