using ECommons.Automation;
using ECommons.Reflection;
using Starloom.Automation;
using System;

namespace Starloom.GameInterop.IPC;

public sealed class ArtisanIpc : IArtisanIpc
{
    private const string PluginName = "Artisan";

    public bool IsAvailable()
        => DalamudReflector.TryGetDalamudPlugin(PluginName, out _, true, false);

    public bool IsListRunning()
        => InvokeFunc("Artisan.IsListRunning", false);

    public bool IsListPaused()
        => InvokeFunc("Artisan.IsListPaused", false);

    public bool IsBusy()
        => InvokeFunc("Artisan.IsBusy", false);

    public bool GetEnduranceStatus()
        => InvokeFunc("Artisan.GetEnduranceStatus", false);

    public bool GetStopRequest()
        => InvokeFunc("Artisan.GetStopRequest", false);

    public void SetListPause(bool paused)
        => InvokeAction("Artisan.SetListPause", paused, requireAvailable: true);

    public void SetStopRequest(bool stop)
        => InvokeAction("Artisan.SetStopRequest", stop, requireAvailable: true);

    public void SetEnduranceStatus(bool enabled)
        => InvokeAction("Artisan.SetEnduranceStatus", enabled, requireAvailable: true);

    public void StartListById(int listId)
    {
        EnsureAvailable();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listId);

        if (IsListRunning())
        {
            if (GetStopRequest())
                SetStopRequest(false);

            if (IsListPaused())
                SetListPause(false);

            return;
        }

        Chat.SendMessage($"/artisan lists {listId} start");
    }

    public ArtisanPauseStatus GetPauseStatus()
        => new(
            IsBusy(),
            IsListRunning(),
            IsListPaused(),
            GetStopRequest(),
            GetEnduranceStatus());

    private void EnsureAvailable()
    {
        if (!IsAvailable())
            throw new InvalidOperationException($"{PluginName} IPC is unavailable.");
    }

    private TResult InvokeFunc<TResult>(string name, TResult failureResult, bool requireAvailable = false)
    {
        if (!IsAvailable())
        {
            if (requireAvailable)
                throw new InvalidOperationException($"{PluginName} IPC is unavailable.");

            return failureResult;
        }

        try
        {
            return Svc.PluginInterface.GetIpcSubscriber<TResult>(name).InvokeFunc();
        }
        catch (Exception ex)
        {
            if (requireAvailable)
                throw new InvalidOperationException($"{PluginName} IPC call failed: {name}", ex);

            return failureResult;
        }
    }

    private void InvokeAction<T>(string name, T arg, bool requireAvailable = false)
    {
        if (!IsAvailable())
        {
            if (requireAvailable)
                throw new InvalidOperationException($"{PluginName} IPC is unavailable.");

            return;
        }

        try
        {
            Svc.PluginInterface.GetIpcSubscriber<T, object>(name).InvokeAction(arg);
        }
        catch (Exception ex)
        {
            if (requireAvailable)
                throw new InvalidOperationException($"{PluginName} IPC call failed: {name}", ex);
        }
    }
}
