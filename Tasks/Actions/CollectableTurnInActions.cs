namespace Starloom.Tasks.Actions;

internal static class CollectableTurnInActions
{
    internal static void Enqueue()
    {
        P.TM.Enqueue(() =>
        {
            P.CollectableTurnIn.Start();
            return true;
        }, "TurnIn.Start");

        P.TM.Enqueue(() =>
        {
            P.CollectableTurnIn.Advance();

            if (P.CollectableTurnIn.HasFailed)
            {
                DuoLog.Error(P.CollectableTurnIn.ErrorMessage ?? "Collectable turn-in failed.");
                return null;
            }

            return P.CollectableTurnIn.IsCompleted ? true : false;
        }, int.MaxValue, true, "TurnIn.Execute");
    }
}
