using Starloom.Services;

namespace Starloom.Tasks;

internal static class TaskCollectableTurnIn
{
    internal static void Enqueue()
    {
        P.TM.Enqueue(() => { P.CollectableTurnIn.Start(); return true; }, "TurnIn.Start");
        P.TM.Enqueue(WaitForCompletion, int.MaxValue, true, "TurnIn");
    }

    private static bool? WaitForCompletion()
    {
        return P.CollectableTurnIn.State switch
        {
            CollectableTurnInState.Done => true,
            CollectableTurnInState.Failed => ReportFailure(),
            _ => false,
        };
    }

    private static bool? ReportFailure()
    {
        DuoLog.Error(P.CollectableTurnIn.ErrorMessage ?? "Collectable turn-in failed.");
        return null;
    }
}
