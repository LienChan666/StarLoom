namespace StarLoom.Tasks.Artisan;

public readonly record struct ArtisanSnapshot(
    bool isAvailable,
    bool isListRunning,
    bool isPaused,
    bool hasStopRequest,
    bool isBusy,
    int listId)
{
    public bool isReady => isAvailable && listId > 0;
}
