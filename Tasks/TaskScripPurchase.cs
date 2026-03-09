using Starloom.Services;

namespace Starloom.Tasks;

internal static class TaskScripPurchase
{
    internal static void Enqueue()
    {
        P.TM.Enqueue(() => { P.ScripPurchase.Start(); return true; }, "ScripPurchase.Start");
        P.TM.Enqueue(WaitForCompletion, int.MaxValue, true, "ScripPurchase");
    }

    private static bool? WaitForCompletion()
    {
        return P.ScripPurchase.State switch
        {
            ScripPurchasePhase.Done => true,
            ScripPurchasePhase.Failed => ReportFailure(),
            _ => false,
        };
    }

    private static bool? ReportFailure()
    {
        DuoLog.Error(P.ScripPurchase.ErrorMessage ?? "Scrip purchase failed.");
        return null;
    }
}
