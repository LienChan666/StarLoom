using ECommons.Automation;
using StarLoom.Core;
using System;

namespace StarLoom.IPC;

public sealed class ArtisanIPC : IArtisanIpc
{
    private readonly IpcCallRunner _ipcCallRunner = new(nameof(ArtisanIPC), "Artisan");

    public bool IsAvailable()
        => _ipcCallRunner.IsAvailable();

    public bool IsListRunning()
        => _ipcCallRunner.InvokeFunc("Artisan.IsListRunning", false);

    public bool IsListPaused()
        => _ipcCallRunner.InvokeFunc("Artisan.IsListPaused", false);

    public bool IsBusy()
        => _ipcCallRunner.InvokeFunc("Artisan.IsBusy", false);

    public bool GetEnduranceStatus()
        => _ipcCallRunner.InvokeFunc("Artisan.GetEnduranceStatus", false);

    public bool GetStopRequest()
        => _ipcCallRunner.InvokeFunc("Artisan.GetStopRequest", false);

    public void SetListPause(bool paused)
        => _ipcCallRunner.InvokeAction("Artisan.SetListPause", paused, requireAvailable: true);

    public void SetStopRequest(bool stop)
        => _ipcCallRunner.InvokeAction("Artisan.SetStopRequest", stop, requireAvailable: true);

    public void SetEnduranceStatus(bool enabled)
        => _ipcCallRunner.InvokeAction("Artisan.SetEnduranceStatus", enabled, requireAvailable: true);

    public void StartListById(int listId)
    {
        _ipcCallRunner.EnsureAvailable();
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
}
