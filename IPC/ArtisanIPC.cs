using ECommons.Automation;
using ECommons.DalamudServices;
using Starloom.Core;
using System;

namespace Starloom.IPC;

public sealed class ArtisanIPC
{
    public bool IsAvailable()
        => ExternalPluginDetector.IsAvailable("Artisan");

    public bool IsListRunning() => InvokeFunc<bool>("Artisan.IsListRunning");
    public bool IsListPaused() => InvokeFunc<bool>("Artisan.IsListPaused");
    public bool IsBusy() => InvokeFunc<bool>("Artisan.IsBusy");
    public bool GetEnduranceStatus() => InvokeFunc<bool>("Artisan.GetEnduranceStatus");
    public bool GetStopRequest() => InvokeFunc<bool>("Artisan.GetStopRequest");

    public void SetListPause(bool paused) => InvokeAction("Artisan.SetListPause", paused);
    public void SetStopRequest(bool stop) => InvokeAction("Artisan.SetStopRequest", stop);
    public void SetEnduranceStatus(bool enabled) => InvokeAction("Artisan.SetEnduranceStatus", enabled);

    public void StartListById(int listId)
    {
        EnsureAvailable();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listId);
        Chat.SendMessage($"/artisan lists {listId} start");
    }

    public ArtisanPauseStatus GetPauseStatus()
        => new(
            IsBusy(),
            IsListRunning(),
            IsListPaused(),
            GetStopRequest(),
            GetEnduranceStatus());

    private T InvokeFunc<T>(string name)
    {
        EnsureAvailable();
        return Svc.PluginInterface.GetIpcSubscriber<T>(name).InvokeFunc();
    }

    private void InvokeAction<T>(string name, T arg)
    {
        EnsureAvailable();
        Svc.PluginInterface.GetIpcSubscriber<T, object>(name).InvokeAction(arg);
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable())
            throw new InvalidOperationException("Artisan IPC is unavailable.");
    }
}
