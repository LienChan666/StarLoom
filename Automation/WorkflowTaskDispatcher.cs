using Starloom.Data;
using Starloom.Tasks;

namespace Starloom.Automation;

internal sealed class WorkflowTaskDispatcher
{
    internal void DispatchStartReturn()
    {
        TaskReturnToCraftPoint.Enqueue();
    }

    internal void DispatchConfiguredWorkflow(bool artisanListManaged)
    {
        EnqueueStartArtisanList(artisanListManaged);
    }

    internal void DispatchLoopTurnInAndPurchase()
    {
        TaskArtisanPause.EnqueueIfNeeded();
        TaskCollectableTurnIn.Enqueue();

        if (P.PurchaseResolver.HasPending())
            TaskScripPurchase.Enqueue();
    }

    internal void DispatchFinalCompletion()
    {
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
        DispatchFinalCompletion();
    }

    private void EnqueuePostAction()
    {
        if (C.PostPurchaseAction == PurchaseCompletionAction.CloseGame)
            TaskCloseGame.Enqueue();
        else
            TaskReturnToCraftPoint.Enqueue();
    }

    private static void EnqueueStartArtisanList(bool artisanListManaged)
    {
        P.TM.Enqueue(() => StartArtisanList(artisanListManaged), "Artisan.StartList");
    }

    private static bool? StartArtisanList(bool artisanListManaged)
    {
        if (!WorkflowStartValidator.CanStartArtisanList(artisanListManaged, out var error))
        {
            DuoLog.Error(error);
            return null;
        }

        if (P.Artisan.GetStopRequest())
            P.Artisan.SetStopRequest(false);

        P.Artisan.StartListById(C.ArtisanListId);
        return true;
    }
}
