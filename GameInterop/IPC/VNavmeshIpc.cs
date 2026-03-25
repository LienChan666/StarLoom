using System.Numerics;

namespace Starloom.GameInterop.IPC;

public static class VNavmeshIpc
{
    private static readonly IpcCallRunner IpcCallRunner = new(nameof(VNavmeshIpc), "vnavmesh");

    public static bool IsAvailable()
        => IpcCallRunner.IsAvailable();

    public static bool PathfindAndMoveTo(Vector3 destination, bool fly)
        => IpcCallRunner.InvokeFunc("vnavmesh.SimpleMove.PathfindAndMoveTo", destination, fly, false);

    public static bool IsPathRunning()
        => IpcCallRunner.InvokeFunc("vnavmesh.Path.IsRunning", false);

    public static void Stop()
        => IpcCallRunner.InvokeAction("vnavmesh.Path.Stop");
}
