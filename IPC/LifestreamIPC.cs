using ECommons.DalamudServices;
using System;

namespace Starloom.IPC;

public static class LifestreamIPC
{
    public static bool IsAvailable()
        => ExternalPluginDetector.IsAvailable("Lifestream");

    public static void ExecuteCommand(string command)
    {
        if (!IsAvailable())
            return;

        try
        {
            Svc.PluginInterface.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand").InvokeAction(command);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[LifestreamIPC] ExecuteCommand failed: {ex}");
        }
    }

    public static bool IsBusy()
    {
        if (!IsAvailable())
            return false;

        try
        {
            return Svc.PluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    public static void Abort()
    {
        if (!IsAvailable())
            return;

        try
        {
            Svc.PluginInterface.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[LifestreamIPC] Abort failed: {ex}");
        }
    }

    public static void EnqueueInnShortcut(int? mode = null)
    {
        if (!IsAvailable())
            return;

        try
        {
            Svc.PluginInterface.GetIpcSubscriber<int?, object>("Lifestream.EnqueueInnShortcut").InvokeAction(mode);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[LifestreamIPC] EnqueueInnShortcut failed: {ex}");
        }
    }
}
