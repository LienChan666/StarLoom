using ECommons.Reflection;
using System;
using System.Numerics;

namespace Starloom.GameInterop.IPC;

public static class VNavmeshIpc
{
    private const string PluginName = "vnavmesh";

    public static bool IsAvailable()
        => DalamudReflector.TryGetDalamudPlugin(PluginName, out _, true, false);

    public static bool PathfindAndMoveTo(Vector3 destination, bool fly)
        => InvokeFunc("vnavmesh.SimpleMove.PathfindAndMoveTo", destination, fly, false);

    public static bool IsPathRunning()
        => InvokeFunc("vnavmesh.Path.IsRunning", false);

    public static void Stop()
        => InvokeAction("vnavmesh.Path.Stop");

    private static TResult InvokeFunc<TResult>(string name, TResult failureResult)
    {
        if (!IsAvailable())
            return failureResult;

        try
        {
            return Svc.PluginInterface.GetIpcSubscriber<TResult>(name).InvokeFunc();
        }
        catch (Exception)
        {
            return failureResult;
        }
    }

    private static TResult InvokeFunc<T1, T2, TResult>(string name, T1 arg1, T2 arg2, TResult failureResult)
    {
        if (!IsAvailable())
            return failureResult;

        try
        {
            return Svc.PluginInterface.GetIpcSubscriber<T1, T2, TResult>(name).InvokeFunc(arg1, arg2);
        }
        catch (Exception)
        {
            return failureResult;
        }
    }

    private static void InvokeAction(string name)
    {
        if (!IsAvailable())
            return;

        try
        {
            Svc.PluginInterface.GetIpcSubscriber<object>(name).InvokeAction();
        }
        catch (Exception)
        {
        }
    }
}
