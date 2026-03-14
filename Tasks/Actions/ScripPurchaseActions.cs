namespace Starloom.Tasks.Actions;

internal static class ScripPurchaseActions
{
    internal static void Enqueue()
    {
        P.TM.Enqueue(() =>
        {
            P.ScripPurchase.Start();
            return true;
        }, "ScripPurchase.Start");

        P.TM.Enqueue(() =>
        {
            P.ScripPurchase.Advance();

            if (P.ScripPurchase.HasFailed)
            {
                DuoLog.Error(P.ScripPurchase.ErrorMessage ?? "Scrip purchase failed.");
                return null;
            }

            return P.ScripPurchase.IsCompleted ? true : false;
        }, int.MaxValue, true, "ScripPurchase.Execute");
    }
}
