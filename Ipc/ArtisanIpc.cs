using ECommons.Automation;

namespace StarLoom.Ipc;

public sealed class ArtisanIpc : IArtisanIpc
{
    private readonly IpcCallRunner ipcCallRunner = new(nameof(ArtisanIpc), "Artisan");

    public bool IsAvailable()
    {
        return ipcCallRunner.IsAvailable();
    }

    public bool IsListRunning()
    {
        return ipcCallRunner.InvokeFunc("Artisan.IsListRunning", false);
    }

    public bool IsListPaused()
    {
        return ipcCallRunner.InvokeFunc("Artisan.IsListPaused", false);
    }

    public bool IsBusy()
    {
        return ipcCallRunner.InvokeFunc("Artisan.IsBusy", false);
    }

    public bool GetStopRequest()
    {
        return ipcCallRunner.InvokeFunc("Artisan.GetStopRequest", false);
    }

    public void SetListPause(bool paused)
    {
        ipcCallRunner.InvokeAction("Artisan.SetListPause", paused, requireAvailable: true);
    }

    public void SetStopRequest(bool stop)
    {
        ipcCallRunner.InvokeAction("Artisan.SetStopRequest", stop, requireAvailable: true);
    }

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
}
