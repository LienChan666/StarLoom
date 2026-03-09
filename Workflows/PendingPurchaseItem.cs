namespace StarLoom.Workflows;

public sealed record PendingPurchaseItem(
    uint ItemId,
    string ItemName,
    int RemainingQuantity,
    int ItemCost,
    int Page,
    int SubPage,
    uint CurrencyItemId);
