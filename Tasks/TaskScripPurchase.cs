using Starloom.Tasks.Actions;

namespace Starloom.Tasks;

internal static class TaskScripPurchase
{
    internal static void Enqueue()
    {
        ScripPurchaseActions.Enqueue();
    }
}
