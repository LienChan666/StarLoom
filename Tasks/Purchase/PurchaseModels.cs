namespace StarLoom.Tasks.Purchase;

public readonly record struct PurchaseTarget(
    uint itemId,
    string itemName,
    int targetCount,
    int currentCount,
    int scripCost,
    string page = "",
    string subPage = "",
    uint currencyItemId = 0,
    int index = 0);

public enum PurchaseStage
{
    Idle,
    WaitingForShop,
    SelectingPage,
    SelectingSubPage,
    SelectingItem,
    Purchasing,
    WaitingForPurchase,
    Cleanup,
    Completed,
    Failed,
}

public readonly record struct PendingPurchaseItem(
    uint itemId,
    string itemName,
    int remainingQuantity,
    int itemCost,
    string page,
    string subPage,
    uint currencyItemId,
    int index = 0);

public readonly record struct PurchaseEntry(
    uint itemId,
    string itemName,
    int quantity,
    int itemCost,
    string page,
    string subPage,
    uint currencyItemId,
    int index = 0);
