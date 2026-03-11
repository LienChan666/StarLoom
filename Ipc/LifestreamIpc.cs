namespace StarLoom.Ipc;

public sealed class LifestreamIpc : ILifestreamIpc
{
    private readonly IpcCallRunner ipcCallRunner = new(nameof(LifestreamIpc), "Lifestream");

    public bool IsAvailable()
    {
        return ipcCallRunner.IsAvailable();
    }

    public bool IsBusy()
    {
        return ipcCallRunner.InvokeFunc("Lifestream.IsBusy", false);
    }

    public void ExecuteCommand(string command)
    {
        ipcCallRunner.InvokeAction("Lifestream.ExecuteCommand", command, requireAvailable: true);
    }

    public void Abort()
    {
        ipcCallRunner.InvokeAction("Lifestream.Abort");
    }

    public void EnqueueInnShortcut(int? mode = null)
    {
        ipcCallRunner.InvokeAction("Lifestream.EnqueueInnShortcut", mode);
    }
}
