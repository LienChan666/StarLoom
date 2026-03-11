namespace StarLoom.Tasks.TurnIn;

public enum TurnInStage
{
    Idle,
    WaitingForCollectableShop,
    SelectingJob,
    SelectingItem,
    Submitting,
    WaitingForSubmit,
    Cleanup,
    Completed,
    Failed,
}

public readonly record struct TurnInCandidate(
    uint itemId,
    string itemName,
    int quantity,
    bool isCollectable,
    uint jobId = 0);

public readonly record struct TurnInEntry(
    uint itemId,
    string itemName,
    int quantity,
    uint jobId);
