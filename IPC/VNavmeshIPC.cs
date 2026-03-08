using ECommons.DalamudServices;
using System;
using System.Numerics;

namespace Starloom.IPC;

public static class VNavmeshIPC
{
    public static bool IsAvailable()
        => ExternalPluginDetector.IsAvailable("vnavmesh");

    public static bool PathfindAndMoveTo(Vector3 destination, bool fly)
    {
        if (!IsAvailable())
            return false;

        try
        {
            return Svc.PluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(destination, fly);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[VNavmeshIPC] PathfindAndMoveTo failed: {ex}");
            return false;
        }
    }

    public static bool IsPathRunning()
    {
        if (!IsAvailable())
            return false;

        try
        {
            return Svc.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    public static void Stop()
    {
        if (!IsAvailable())
            return;

        try
        {
            Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[VNavmeshIPC] Stop failed: {ex}");
        }
    }
}
