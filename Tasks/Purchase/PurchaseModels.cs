namespace StarLoom.Tasks.Purchase;

public readonly record struct PurchaseTarget(
    uint itemId,
    string itemName,
    int targetCount,
    int currentCount,
    int scripCost,
    string page = "",
    string subPage = "");

public readonly record struct PurchaseEntry(
    uint itemId,
    string itemName,
    int quantity,
    int scripCost,
    string page,
    string subPage);
