using Starloom.Core;

namespace Starloom.IPC;

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
    void SetEnduranceStatus(bool enabled);
    void StartListById(int listId);
    ArtisanPauseStatus GetPauseStatus();
}
