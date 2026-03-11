using System.Numerics;

namespace StarLoom.Ipc;

public sealed class VNavmeshIpc : IVNavmeshIpc
{
    private readonly IpcCallRunner ipcCallRunner = new(nameof(VNavmeshIpc), "vnavmesh");

    public bool IsAvailable()
    {
        return ipcCallRunner.IsAvailable();
    }

    public bool PathfindAndMoveTo(Vector3 destination, bool fly)
    {
        return ipcCallRunner.InvokeFunc("vnavmesh.SimpleMove.PathfindAndMoveTo", destination, fly, false);
    }

    public bool IsPathRunning()
    {
        return ipcCallRunner.InvokeFunc("vnavmesh.Path.IsRunning", false);
    }

    public void Stop()
    {
        ipcCallRunner.InvokeAction("vnavmesh.Path.Stop");
    }
}
