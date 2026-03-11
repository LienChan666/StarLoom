using StarLoom.Config;
using StarLoom.Game;

namespace StarLoom.Tasks.Purchase;

public sealed class PurchaseTask
{
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ShopInteractionDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ShopWindowTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PurchaseTimeout = TimeSpan.FromSeconds(5);

    private readonly PluginConfig pluginConfig;
    private readonly InventoryGame inventoryGame;
    private readonly NpcGame npcGame;
    private readonly ScripShopGame scripShopGame;
    private readonly PendingPurchaseResolver pendingPurchaseResolver;
    private readonly Func<DateTime> getUtcNow;

    private List<PurchaseEntry> queue = [];
    private PurchaseStage purchaseStage = PurchaseStage.Idle;
    private int currentIndex;
    private int currentPurchaseAmount;
    private uint currentTargetItemId;
    private string currentTargetItemName = string.Empty;
    private int inventoryCountBeforePurchase;
    private DateTime lastActionAt = DateTime.MinValue;
    private DateTime stageEnteredAt = DateTime.MinValue;
    private DateTime purchaseStartedAt = DateTime.MinValue;

    public string currentStage => purchaseStage.ToString();
    public bool isRunning { get; private set; }
    public bool isCompleted { get; private set; }
    public bool hasFailed { get; private set; }
    public string? errorMessage { get; private set; }

    public PurchaseTask()
        : this(CreateDefaultPluginConfig(), CreateDefaultInventoryGame())
    {
    }

    private PurchaseTask(PluginConfig pluginConfig, InventoryGame inventoryGame)
        : this(
            pluginConfig,
            inventoryGame,
            new NpcGame(),
            new ScripShopGame(),
            new PendingPurchaseResolver(pluginConfig, inventoryGame))
    {
    }

    public PurchaseTask(
        PluginConfig pluginConfig,
        InventoryGame inventoryGame,
        NpcGame npcGame,
        ScripShopGame scripShopGame,
        PendingPurchaseResolver pendingPurchaseResolver,
        Func<DateTime>? getUtcNow = null)
    {
        this.pluginConfig = pluginConfig;
        this.inventoryGame = inventoryGame;
        this.npcGame = npcGame;
        this.scripShopGame = scripShopGame;
        this.pendingPurchaseResolver = pendingPurchaseResolver;
        this.getUtcNow = getUtcNow ?? (() => DateTime.UtcNow);
    }

    public IReadOnlyList<PurchaseEntry> GetQueue()
    {
        return queue;
    }

    public void Start()
    {
        inventoryGame.InvalidateTransientCaches();
        Start(pendingPurchaseResolver.ResolvePendingTargets());
    }

    public void Start(IEnumerable<PendingPurchaseItem> pendingItems)
    {
        Stop();

        queue = PurchasePlan.BuildQueue(pendingItems);
        currentIndex = 0;
        currentPurchaseAmount = 0;
        currentTargetItemId = 0;
        currentTargetItemName = string.Empty;
        inventoryCountBeforePurchase = 0;
        lastActionAt = DateTime.MinValue;
        purchaseStartedAt = DateTime.MinValue;
        stageEnteredAt = getUtcNow();
        isRunning = true;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
        purchaseStage = queue.Count == 0 ? PurchaseStage.Completed : PurchaseStage.WaitingForShop;

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

        switch (purchaseStage)
        {
            case PurchaseStage.WaitingForShop:
                HandleWaitingForShop();
                return;
            case PurchaseStage.SelectingPage:
                HandleSelectingPage();
                return;
            case PurchaseStage.SelectingSubPage:
                HandleSelectingSubPage();
                return;
            case PurchaseStage.SelectingItem:
                HandleSelectingItem();
                return;
            case PurchaseStage.Purchasing:
                HandlePurchasing();
                return;
            case PurchaseStage.WaitingForPurchase:
                HandleWaitingForPurchase();
                return;
            case PurchaseStage.Cleanup:
                HandleCleanup();
                return;
        }
    }

    public void Stop()
    {
        if (purchaseStage is not PurchaseStage.Idle and not PurchaseStage.Completed and not PurchaseStage.Failed)
            scripShopGame.CloseShop();

        queue = [];
        currentIndex = 0;
        currentPurchaseAmount = 0;
        currentTargetItemId = 0;
        currentTargetItemName = string.Empty;
        inventoryCountBeforePurchase = 0;
        lastActionAt = DateTime.MinValue;
        stageEnteredAt = DateTime.MinValue;
        purchaseStartedAt = DateTime.MinValue;
        purchaseStage = PurchaseStage.Idle;
        isRunning = false;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
    }

    private void HandleWaitingForShop()
    {
        if (scripShopGame.IsReady())
        {
            TransitionTo(PurchaseStage.SelectingPage);
            return;
        }

        if ((getUtcNow() - stageEnteredAt) > ShopWindowTimeout)
        {
            Fail("Timed out while waiting for the scrip shop window.");
            return;
        }

        if ((getUtcNow() - lastActionAt) < ShopInteractionDelay)
            return;

        if (scripShopGame.OpenShop())
        {
            lastActionAt = getUtcNow();
            return;
        }

        var shop = pluginConfig.preferredCollectableShop;
        if (shop == null)
        {
            Fail("A collectable shop must be configured before starting.");
            return;
        }

        if (npcGame.TryInteract(shop.scripShopNpcId))
            lastActionAt = getUtcNow();
    }

    private void HandleSelectingPage()
    {
        if (currentIndex >= queue.Count)
        {
            TransitionTo(PurchaseStage.Cleanup);
            return;
        }

        if (!ActionDelayElapsed())
            return;

        var entry = queue[currentIndex];
        scripShopGame.SelectPage(entry.page);
        lastActionAt = getUtcNow();
        TransitionTo(PurchaseStage.SelectingSubPage);
    }

    private void HandleSelectingSubPage()
    {
        if (currentIndex >= queue.Count)
        {
            TransitionTo(PurchaseStage.Cleanup);
            return;
        }

        if (!ActionDelayElapsed())
            return;

        var entry = queue[currentIndex];
        scripShopGame.SelectSubPage(entry.subPage);
        lastActionAt = getUtcNow();
        TransitionTo(PurchaseStage.SelectingItem);
    }

    private void HandleSelectingItem()
    {
        if (currentIndex >= queue.Count)
        {
            TransitionTo(PurchaseStage.Cleanup);
            return;
        }

        if (!ActionDelayElapsed())
            return;

        var entry = queue[currentIndex];
        var currentCurrencyCount = inventoryGame.GetCurrencyCount(entry.currencyItemId);
        if (currentCurrencyCount < 0)
        {
            Fail($"Could not read scrip count for {entry.itemName}");
            return;
        }

        var quantity = PurchasePlan.ResolvePurchaseQuantity(entry, currentCurrencyCount, pluginConfig.reserveScripAmount);
        if (quantity <= 0)
        {
            currentIndex++;
            TransitionTo(currentIndex < queue.Count ? PurchaseStage.SelectingPage : PurchaseStage.Cleanup);
            return;
        }

        inventoryGame.InvalidateTransientCaches();
        inventoryCountBeforePurchase = inventoryGame.GetItemCount(entry.itemId);
        if (!scripShopGame.SelectItem(entry, quantity))
        {
            Fail($"Could not locate the item in the scrip shop: {entry.itemName}");
            return;
        }

        currentPurchaseAmount = quantity;
        currentTargetItemId = entry.itemId;
        currentTargetItemName = entry.itemName;
        purchaseStartedAt = DateTime.MinValue;
        lastActionAt = getUtcNow();
        TransitionTo(PurchaseStage.Purchasing);
    }

    private void HandlePurchasing()
    {
        if (!ActionDelayElapsed())
            return;

        var result = scripShopGame.ConfirmPurchase(currentTargetItemId, currentTargetItemName);
        switch (result)
        {
            case PurchaseDialogResult.Missing:
                if (purchaseStartedAt == DateTime.MinValue)
                    purchaseStartedAt = getUtcNow();

                if ((getUtcNow() - purchaseStartedAt) > PurchaseTimeout)
                    Fail($"Purchase confirmation window did not appear: {currentTargetItemName}");

                return;
            case PurchaseDialogResult.MismatchedItem:
                Fail($"Purchase confirmation item mismatch: {currentTargetItemName}");
                return;
            case PurchaseDialogResult.Confirmed:
                purchaseStartedAt = getUtcNow();
                lastActionAt = getUtcNow();
                TransitionTo(PurchaseStage.WaitingForPurchase);
                return;
        }
    }

    private void HandleWaitingForPurchase()
    {
        if (!ActionDelayElapsed())
            return;

        inventoryGame.InvalidateTransientCaches();
        var currentCount = inventoryGame.GetItemCount(currentTargetItemId);
        if (currentCount <= inventoryCountBeforePurchase)
        {
            if ((getUtcNow() - purchaseStartedAt) > PurchaseTimeout)
                Fail($"Scrip purchase did not complete: {currentTargetItemName}");

            return;
        }

        var entry = queue[currentIndex];
        var remainingQuantity = entry.quantity - currentPurchaseAmount;
        if (remainingQuantity > 0)
            queue[currentIndex] = entry with { quantity = remainingQuantity };
        else
            currentIndex++;

        currentPurchaseAmount = 0;
        currentTargetItemId = 0;
        currentTargetItemName = string.Empty;
        inventoryCountBeforePurchase = 0;
        purchaseStartedAt = DateTime.MinValue;
        lastActionAt = getUtcNow();
        TransitionTo(currentIndex < queue.Count ? PurchaseStage.SelectingPage : PurchaseStage.Cleanup);
    }

    private void HandleCleanup()
    {
        scripShopGame.CloseShop();
        inventoryGame.InvalidateTransientCaches();
        isRunning = false;
        isCompleted = true;
        hasFailed = false;
        purchaseStage = PurchaseStage.Completed;
    }

    private bool ActionDelayElapsed()
    {
        return (getUtcNow() - lastActionAt) >= ActionDelay;
    }

    private void TransitionTo(PurchaseStage nextStage)
    {
        purchaseStage = nextStage;
        stageEnteredAt = getUtcNow();
    }

    private void Fail(string message)
    {
        scripShopGame.CloseShop();
        errorMessage = message;
        hasFailed = true;
        isRunning = false;
        isCompleted = false;
        purchaseStage = PurchaseStage.Failed;
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

    private static PluginConfig CreateDefaultPluginConfig()
    {
        return new PluginConfig();
    }

    private static InventoryGame CreateDefaultInventoryGame()
    {
        return new InventoryGame();
    }
}
