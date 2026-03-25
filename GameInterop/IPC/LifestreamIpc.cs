using ECommons.Reflection;
using System;

namespace Starloom.GameInterop.IPC;

public static class LifestreamIpc
{
    private const string PluginName = "Lifestream";

    public static bool IsAvailable()
        => DalamudReflector.TryGetDalamudPlugin(PluginName, out _, true, false);

    public static void ExecuteCommand(string command)
        => InvokeAction("Lifestream.ExecuteCommand", command);

    public static bool IsBusy()
        => InvokeFunc("Lifestream.IsBusy", false);

    public static void Abort()
        => InvokeAction("Lifestream.Abort");

    public static void EnqueueInnShortcut(int? mode = null)
        => InvokeAction("Lifestream.EnqueueInnShortcut", mode);

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

    private static void InvokeAction<T>(string name, T arg)
    {
        if (!IsAvailable())
            return;

        try
        {
            Svc.PluginInterface.GetIpcSubscriber<T, object>(name).InvokeAction(arg);
        }
        catch (Exception)
        {
        }
    }
}
