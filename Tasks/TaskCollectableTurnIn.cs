using Starloom.Tasks.Actions;

namespace Starloom.Tasks;

internal static class TaskCollectableTurnIn
{
    internal static void Enqueue()
    {
        CollectableTurnInActions.Enqueue();
    }
}
