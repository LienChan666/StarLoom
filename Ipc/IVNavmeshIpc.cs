using System.Numerics;

namespace StarLoom.Ipc;

public interface IVNavmeshIpc
{
    bool IsAvailable();
    bool PathfindAndMoveTo(Vector3 destination, bool fly);
    bool IsPathRunning();
    void Stop();
}
