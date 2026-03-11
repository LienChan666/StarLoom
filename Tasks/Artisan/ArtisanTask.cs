using StarLoom.Config;
using StarLoom.Ipc;

namespace StarLoom.Tasks.Artisan;

public sealed class ArtisanTask
{
    private readonly IArtisanIpc artisanIpc;
    private readonly PluginConfig pluginConfig;

    private ArtisanSnapshot snapshot;

    public ArtisanTask() : this(new ArtisanIpc(), new PluginConfig())
    {
    }

    public ArtisanTask(IArtisanIpc artisanIpc, PluginConfig pluginConfig)
    {
        this.artisanIpc = artisanIpc;
        this.pluginConfig = pluginConfig;
        snapshot = ReadSnapshot();
    }

    public bool CanControl(out string errorMessage)
    {
        if (!snapshot.isAvailable)
        {
            errorMessage = "Artisan IPC is unavailable.";
            return false;
        }

        if (pluginConfig.artisanListId <= 0)
        {
            errorMessage = "Artisan list is not configured.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool StartConfiguredList()
    {
        Update();

        if (!CanControl(out var errorMessage))
        {
            TryDuoLogError(errorMessage);
            return false;
        }

        artisanIpc.StartListById(pluginConfig.artisanListId);
        Update();
        return true;
    }

    public void RequestPause()
    {
        Update();

        if (!snapshot.isAvailable || snapshot.isPaused || !snapshot.isListRunning)
            return;

        artisanIpc.SetListPause(true);
        Update();
    }

    public void Pause()
    {
        Update();

        if (!snapshot.isAvailable || !snapshot.isListRunning || snapshot.isPaused)
            return;

        artisanIpc.SetListPause(true);
        Update();
    }

    public void Resume()
    {
        Update();

        if (!snapshot.isAvailable || !snapshot.isListRunning || !snapshot.isPaused)
            return;

        artisanIpc.SetListPause(false);
        Update();
    }

    public void Stop()
    {
        Update();

        if (!snapshot.isAvailable)
            return;

        artisanIpc.SetStopRequest(true);
        Update();
    }

    public void Update()
    {
        snapshot = ReadSnapshot();
    }

    public ArtisanSnapshot GetSnapshot()
    {
        return snapshot;
    }

    private ArtisanSnapshot ReadSnapshot()
    {
        var isAvailable = artisanIpc.IsAvailable();
        if (!isAvailable)
            return new ArtisanSnapshot(false, false, false, false, false, pluginConfig.artisanListId);

        return new ArtisanSnapshot(
            isAvailable,
            artisanIpc.IsListRunning(),
            artisanIpc.IsListPaused(),
            artisanIpc.GetStopRequest(),
            artisanIpc.IsBusy(),
            pluginConfig.artisanListId);
    }

    private static void TryDuoLogError(string message)
    {
        try
        {
            DuoLog.Error(message);
        }
        catch
        {
        }
    }
}
