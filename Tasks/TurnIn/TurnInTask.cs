using StarLoom.Game;

namespace StarLoom.Tasks.TurnIn;

public sealed class TurnInTask
{
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SubmitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OvercapCheckWindow = TimeSpan.FromMilliseconds(500);

    private readonly InventoryGame inventoryGame;
    private readonly CollectableShopGame collectableShopGame;
    private readonly TurnInJobResolver turnInJobResolver;
    private readonly Func<DateTime> getUtcNow;

    private List<TurnInEntry> queue = [];
    private TurnInStage turnInStage = TurnInStage.Idle;
    private uint currentItemId;
    private uint currentJobId;
    private int currentIndex;
    private int inventoryCountBeforeSubmit;
    private DateTime lastActionAt = DateTime.MinValue;
    private DateTime submitStartedAt = DateTime.MinValue;
    private bool overcapDetected;

    public string currentStage => turnInStage.ToString();
    public bool isRunning { get; private set; }
    public bool isCompleted { get; private set; }
    public bool hasFailed { get; private set; }
    public string? errorMessage { get; private set; }

    public TurnInTask() : this(new InventoryGame(), new CollectableShopGame(), new TurnInJobResolver())
    {
    }

    public TurnInTask(
        InventoryGame inventoryGame,
        CollectableShopGame collectableShopGame,
        TurnInJobResolver turnInJobResolver,
        Func<DateTime>? getUtcNow = null)
    {
        this.inventoryGame = inventoryGame;
        this.collectableShopGame = collectableShopGame;
        this.turnInJobResolver = turnInJobResolver;
        this.getUtcNow = getUtcNow ?? (() => DateTime.UtcNow);
    }

    public IReadOnlyList<TurnInEntry> GetQueue()
    {
        return queue;
    }

    public void Start()
    {
        inventoryGame.InvalidateTransientCaches();
        var candidates = inventoryGame
            .GetInventoryItems()
            .Where(item => item.isCollectable && inventoryGame.IsCollectableTurnInItem(item.itemId))
            .Select(item =>
            {
                var itemName = string.IsNullOrWhiteSpace(item.itemName)
                    ? inventoryGame.GetItemName(item.itemId)
                    : item.itemName;
                var jobId = turnInJobResolver.ResolveJobId(item.itemId, itemName);
                return new TurnInCandidate(item.itemId, itemName, item.quantity, item.isCollectable, jobId);
            });

        Start(candidates);
    }

    public void Start(IEnumerable<TurnInCandidate> candidates)
    {
        Stop();

        queue = TurnInPlan.BuildQueue(candidates);
        currentIndex = 0;
        currentItemId = 0;
        currentJobId = 0;
        inventoryCountBeforeSubmit = 0;
        lastActionAt = DateTime.MinValue;
        submitStartedAt = DateTime.MinValue;
        overcapDetected = false;
        isRunning = true;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
        turnInStage = queue.Count == 0 ? TurnInStage.Completed : TurnInStage.WaitingForCollectableShop;

        if (queue.Count == 0)
        {
            isRunning = false;
            isCompleted = true;
        }
    }

    public void Update()
    {
        if (!isRunning || isCompleted || hasFailed)
            return;

        switch (turnInStage)
        {
            case TurnInStage.WaitingForCollectableShop:
                HandleWaitingForCollectableShop();
                return;
            case TurnInStage.SelectingJob:
                HandleSelectingJob();
                return;
            case TurnInStage.SelectingItem:
                HandleSelectingItem();
                return;
            case TurnInStage.Submitting:
                HandleSubmitting();
                return;
            case TurnInStage.WaitingForSubmit:
                HandleWaitingForSubmit();
                return;
            case TurnInStage.Cleanup:
                HandleCleanup();
                return;
        }
    }

    public void Stop()
    {
        if (turnInStage is not TurnInStage.Idle and not TurnInStage.Completed and not TurnInStage.Failed)
            collectableShopGame.CloseWindow();

        queue = [];
        currentIndex = 0;
        currentItemId = 0;
        currentJobId = 0;
        inventoryCountBeforeSubmit = 0;
        lastActionAt = DateTime.MinValue;
        submitStartedAt = DateTime.MinValue;
        overcapDetected = false;
        turnInStage = TurnInStage.Idle;
        isRunning = false;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
    }

    private void HandleWaitingForCollectableShop()
    {
        if (!collectableShopGame.IsReady())
            return;

        turnInStage = TurnInStage.SelectingJob;
    }

    private void HandleSelectingJob()
    {
        if (currentIndex >= queue.Count || overcapDetected)
        {
            turnInStage = TurnInStage.Cleanup;
            return;
        }

        if (!ActionDelayElapsed())
            return;

        var entry = queue[currentIndex];
        if (currentJobId != entry.jobId)
        {
            collectableShopGame.SelectJob(entry.jobId);
            currentJobId = entry.jobId;
            lastActionAt = getUtcNow();
        }

        turnInStage = TurnInStage.SelectingItem;
    }

    private void HandleSelectingItem()
    {
        if (currentIndex >= queue.Count || overcapDetected)
        {
            turnInStage = TurnInStage.Cleanup;
            return;
        }

        if (!ActionDelayElapsed())
            return;

        var entry = queue[currentIndex];
        if (currentItemId != entry.itemId)
        {
            collectableShopGame.SelectItemById(entry.itemId);
            currentItemId = entry.itemId;
            lastActionAt = getUtcNow();
        }

        turnInStage = TurnInStage.Submitting;
    }

    private void HandleSubmitting()
    {
        if (currentIndex >= queue.Count || overcapDetected)
        {
            turnInStage = TurnInStage.Cleanup;
            return;
        }

        if (!ActionDelayElapsed())
            return;

        var entry = queue[currentIndex];
        inventoryGame.InvalidateTransientCaches();
        inventoryCountBeforeSubmit = inventoryGame.GetCollectableInventoryItemCount(entry.itemId);
        collectableShopGame.SubmitItem();
        submitStartedAt = getUtcNow();
        lastActionAt = getUtcNow();
        turnInStage = TurnInStage.WaitingForSubmit;
    }

    private void HandleWaitingForSubmit()
    {
        if (currentIndex >= queue.Count)
        {
            turnInStage = TurnInStage.Cleanup;
            return;
        }

        if ((getUtcNow() - submitStartedAt) < OvercapCheckWindow && collectableShopGame.TryDismissOvercapDialog())
        {
            overcapDetected = true;
            turnInStage = TurnInStage.Cleanup;
            return;
        }

        var entry = queue[currentIndex];
        inventoryGame.InvalidateTransientCaches();
        var currentCount = inventoryGame.GetCollectableInventoryItemCount(entry.itemId);
        if (currentCount >= inventoryCountBeforeSubmit)
        {
            if ((getUtcNow() - submitStartedAt) > SubmitTimeout)
            {
                Fail($"Collectable submission did not complete: {entry.itemName}");
                return;
            }

            return;
        }

        if (entry.quantity > 1)
            queue[currentIndex] = entry with { quantity = entry.quantity - 1 };
        else
            currentIndex++;

        currentItemId = 0;
        inventoryCountBeforeSubmit = 0;
        submitStartedAt = DateTime.MinValue;
        lastActionAt = getUtcNow();
        turnInStage = currentIndex < queue.Count ? TurnInStage.SelectingJob : TurnInStage.Cleanup;
    }

    private void HandleCleanup()
    {
        collectableShopGame.CloseWindow();
        isRunning = false;
        isCompleted = true;
        hasFailed = false;
        turnInStage = TurnInStage.Completed;
    }

    private bool ActionDelayElapsed()
    {
        return (getUtcNow() - lastActionAt) >= ActionDelay;
    }

    private void Fail(string message)
    {
        collectableShopGame.CloseWindow();
        errorMessage = message;
        hasFailed = true;
        isRunning = false;
        isCompleted = false;
        turnInStage = TurnInStage.Failed;
        TryDuoLogError(message);
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
