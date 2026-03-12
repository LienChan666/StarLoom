namespace StarLoom.Ipc;

public interface IArtisanIpc
{
    bool IsAvailable();
    bool IsListRunning();
    bool IsListPaused();
    bool IsBusy();
    bool GetEnduranceStatus();
    bool GetStopRequest();
    void SetListPause(bool paused);
    void SetStopRequest(bool stop);
    void StartListById(int listId);
}
