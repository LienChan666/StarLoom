using Starloom.Data;
using Starloom.Tasks;

namespace Starloom.Automation;

internal sealed class WorkflowTaskDispatcher
{
    internal void DispatchStartReturn()
    {
        TaskReturnToCraftPoint.Enqueue();
    }

    internal void DispatchConfiguredWorkflow()
    {
        TaskArtisanPause.EnqueueIfNeeded();
        TaskCollectableTurnIn.Enqueue();

        if (C.BuyAfterEachTurnIn && P.PurchaseResolver.HasPending())
            TaskScripPurchase.Enqueue();

        EnqueuePostAction();
    }

    internal void DispatchCollectableTurnIn()
    {
        TaskArtisanPause.EnqueueIfNeeded();
        TaskCollectableTurnIn.Enqueue();
    }

    internal void DispatchPurchaseOnly()
    {
        TaskArtisanPause.EnqueueIfNeeded();
        TaskScripPurchase.Enqueue();
        EnqueuePostAction();
    }

    internal void DispatchReturnToCraftPoint()
    {
        TaskReturnToCraftPoint.Enqueue();
    }

    private void EnqueuePostAction()
    {
        if (C.PostPurchaseAction == PurchaseCompletionAction.CloseGame)
            TaskCloseGame.Enqueue();
        else
            DispatchReturnToCraftPoint();
    }
}
