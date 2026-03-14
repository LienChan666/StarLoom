using ECommons.Automation;
using Starloom.Automation;
using System;

namespace Starloom.GameInterop.IPC;

public sealed class ArtisanIpc : IArtisanIpc
{
    private readonly IpcCallRunner ipcCallRunner = new(nameof(ArtisanIpc), "Artisan");

    public bool IsAvailable()
        => ipcCallRunner.IsAvailable();

    public bool IsListRunning()
        => ipcCallRunner.InvokeFunc("Artisan.IsListRunning", false);

    public bool IsListPaused()
        => ipcCallRunner.InvokeFunc("Artisan.IsListPaused", false);

    public bool IsBusy()
        => ipcCallRunner.InvokeFunc("Artisan.IsBusy", false);

    public bool GetEnduranceStatus()
        => ipcCallRunner.InvokeFunc("Artisan.GetEnduranceStatus", false);

    public bool GetStopRequest()
        => ipcCallRunner.InvokeFunc("Artisan.GetStopRequest", false);

    public void SetListPause(bool paused)
        => ipcCallRunner.InvokeAction("Artisan.SetListPause", paused, requireAvailable: true);

    public void SetStopRequest(bool stop)
        => ipcCallRunner.InvokeAction("Artisan.SetStopRequest", stop, requireAvailable: true);

    public void SetEnduranceStatus(bool enabled)
        => ipcCallRunner.InvokeAction("Artisan.SetEnduranceStatus", enabled, requireAvailable: true);

    public void StartListById(int listId)
    {
        ipcCallRunner.EnsureAvailable();
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
}
