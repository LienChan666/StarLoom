namespace StarLoom.IPC;

public static class LifestreamIPC
{
    private static readonly IpcCallRunner IpcCallRunner = new(nameof(LifestreamIPC), "Lifestream");

    public static bool IsAvailable()
        => IpcCallRunner.IsAvailable();

    public static void ExecuteCommand(string command)
        => IpcCallRunner.InvokeAction("Lifestream.ExecuteCommand", command);

    public static bool IsBusy()
        => IpcCallRunner.InvokeFunc("Lifestream.IsBusy", false);

    public static void Abort()
        => IpcCallRunner.InvokeAction("Lifestream.Abort");

    public static void EnqueueInnShortcut(int? mode = null)
        => IpcCallRunner.InvokeAction("Lifestream.EnqueueInnShortcut", mode);
}
