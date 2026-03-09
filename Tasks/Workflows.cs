using Starloom.Data;

namespace Starloom.Tasks;

internal static class Workflows
{
    internal static void EnqueueConfiguredWorkflow()
    {
        TaskArtisanPause.EnqueueIfNeeded();
        TaskCollectableTurnIn.Enqueue();

        if (C.BuyAfterEachTurnIn && P.PurchaseResolver.HasPending())
            TaskScripPurchase.Enqueue();

        EnqueuePostAction();
    }

    internal static void EnqueuePurchaseWorkflow()
    {
        TaskArtisanPause.EnqueueIfNeeded();
        TaskScripPurchase.Enqueue();
        EnqueuePostAction();
    }

    internal static void EnqueueTurnInOnly()
    {
        TaskArtisanPause.EnqueueIfNeeded();
        TaskCollectableTurnIn.Enqueue();
    }

    private static void EnqueuePostAction()
    {
        if (C.PostPurchaseAction == PurchaseCompletionAction.CloseGame)
            TaskCloseGame.Enqueue();
        else
            TaskReturnToCraftPoint.Enqueue();
    }
}
