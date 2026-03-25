using System.Diagnostics;

namespace Starloom.Tasks;

internal static class TaskCloseGame
{
    internal static void Enqueue()
    {
        P.TM.Enqueue(Execute, "CloseGame.Execute");
    }

    private static bool? Execute()
    {
        Process.GetCurrentProcess().Kill();
        return true;
    }
}
