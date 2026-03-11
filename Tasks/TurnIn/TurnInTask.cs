using ECommons.Automation.LegacyTaskManager;
using StarLoom.Game;

namespace StarLoom.Tasks.TurnIn;

public sealed class TurnInTask
{
    private TaskManager? taskManager;
    private readonly InventoryGame inventoryGame;
    private readonly NpcGame npcGame;
    private readonly CollectableShopGame collectableShopGame;

    private List<TurnInEntry> queue = [];
    private int currentIndex;

    public bool isRunning { get; private set; }
    public bool isCompleted { get; private set; }
    public bool hasFailed { get; private set; }
    public string? errorMessage { get; private set; }

    public TurnInTask() : this(new InventoryGame(), new NpcGame(), new CollectableShopGame())
    {
    }

    public TurnInTask(InventoryGame inventoryGame, NpcGame npcGame, CollectableShopGame collectableShopGame)
    {
        this.inventoryGame = inventoryGame;
        this.npcGame = npcGame;
        this.collectableShopGame = collectableShopGame;
    }

    public IReadOnlyList<TurnInEntry> GetQueue()
    {
        return queue;
    }

    public void Start()
    {
        var candidates = inventoryGame
            .GetInventoryItems()
            .Where(item => item.isCollectable)
            .Select(item => new TurnInCandidate(item.itemId, string.Empty, item.quantity, item.isCollectable));

        Start(candidates);
    }

    public void Start(IEnumerable<TurnInCandidate> candidates)
    {
        Stop();

        queue = TurnInPlan.BuildQueue(candidates);
        currentIndex = 0;
        isRunning = true;
        isCompleted = queue.Count == 0;
        hasFailed = false;
        errorMessage = null;

        if (queue.Count == 0)
        {
            isRunning = false;
            return;
        }

        taskManager = new TaskManager();
        taskManager.Enqueue(() => true, "TurnIn.BuildQueue");
        taskManager.Enqueue(WaitForCollectableShop, "TurnIn.OpenCollectableWindow");
        taskManager.Enqueue(ProcessQueue, int.MaxValue, true, "TurnIn.Execute");
        taskManager.Enqueue(Finish, "TurnIn.Cleanup");
    }

    public void Update()
    {
        if (!isRunning || hasFailed)
            return;

        if (taskManager is not { IsBusy: true })
        {
            isRunning = false;
            isCompleted = true;
        }
    }

    public void Stop()
    {
        taskManager?.Abort();
        taskManager = null;
        queue = [];
        currentIndex = 0;
        isRunning = false;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
    }

    private bool? WaitForCollectableShop()
    {
        return collectableShopGame.IsReady() ? true : false;
    }

    private bool? ProcessQueue()
    {
        if (currentIndex >= queue.Count)
            return true;

        var entry = queue[currentIndex];
        collectableShopGame.SelectJob(entry.jobId);
        collectableShopGame.SelectItemById(entry.itemId);
        collectableShopGame.SubmitItem();
        currentIndex++;
        return currentIndex >= queue.Count ? true : false;
    }

    private bool? Finish()
    {
        collectableShopGame.CloseWindow();
        isRunning = false;
        isCompleted = true;
        return true;
    }

    private void Fail(string message)
    {
        errorMessage = message;
        hasFailed = true;
        isRunning = false;
        isCompleted = false;
        taskManager?.Abort();
        taskManager = null;
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
