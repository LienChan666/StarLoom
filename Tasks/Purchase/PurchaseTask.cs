using ECommons.Automation.LegacyTaskManager;
using StarLoom.Config;
using StarLoom.Game;

namespace StarLoom.Tasks.Purchase;

public sealed class PurchaseTask
{
    private TaskManager? taskManager;
    private readonly PluginConfig pluginConfig;
    private readonly InventoryGame inventoryGame;
    private readonly ScripShopGame scripShopGame;

    private List<PurchaseEntry> queue = [];
    private int currentIndex;

    public bool isRunning { get; private set; }
    public bool isCompleted { get; private set; }
    public bool hasFailed { get; private set; }
    public string? errorMessage { get; private set; }

    public PurchaseTask() : this(new PluginConfig(), new InventoryGame(), new ScripShopGame())
    {
    }

    public PurchaseTask(PluginConfig pluginConfig, InventoryGame inventoryGame, ScripShopGame scripShopGame)
    {
        this.pluginConfig = pluginConfig;
        this.inventoryGame = inventoryGame;
        this.scripShopGame = scripShopGame;
    }

    public IReadOnlyList<PurchaseEntry> GetQueue()
    {
        return queue;
    }

    public void Start(int currentScrips)
    {
        var purchaseTargets = pluginConfig.scripShopItems.Select(item => new PurchaseTarget(
            item.itemId,
            item.itemName,
            item.targetCount,
            inventoryGame.GetItemCount(item.itemId),
            item.scripCost,
            item.page,
            item.subPage));

        Start(purchaseTargets, currentScrips, pluginConfig.reserveScripAmount);
    }

    public void Start(IEnumerable<PurchaseTarget> purchaseTargets, int currentScrips, int reserveAmount)
    {
        Stop();

        queue = PurchasePlan.BuildQueue(purchaseTargets, currentScrips, reserveAmount);
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
        taskManager.Enqueue(OpenShop, "Purchase.OpenShop");
        taskManager.Enqueue(ProcessQueue, int.MaxValue, true, "Purchase.Execute");
        taskManager.Enqueue(Finish, "Purchase.Cleanup");
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

    private bool? OpenShop()
    {
        scripShopGame.OpenShop();
        return scripShopGame.IsReady() ? true : false;
    }

    private bool? ProcessQueue()
    {
        if (currentIndex >= queue.Count)
            return true;

        var entry = queue[currentIndex];
        scripShopGame.SelectPage(entry.page);
        scripShopGame.SelectSubPage(entry.subPage);

        if (!scripShopGame.SelectItem(entry.itemId, entry.itemName, entry.quantity))
        {
            Fail($"Failed to select {entry.itemName}.");
            return null;
        }

        if (!scripShopGame.ConfirmPurchase(entry.itemId, entry.itemName))
        {
            Fail($"Failed to confirm purchase for {entry.itemName}.");
            return null;
        }

        currentIndex++;
        return currentIndex >= queue.Count ? true : false;
    }

    private bool? Finish()
    {
        scripShopGame.CloseShop();
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
