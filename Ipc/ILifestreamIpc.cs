namespace StarLoom.Ipc;

public interface ILifestreamIpc
{
    bool IsAvailable();
    bool IsBusy();
    void ExecuteCommand(string command);
    void Abort();
    void EnqueueInnShortcut(int? mode = null);
}
