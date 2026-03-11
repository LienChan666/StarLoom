namespace StarLoom.Tasks.Purchase;

public static class PurchasePlan
{
    public static List<PurchaseEntry> BuildQueue(IEnumerable<PendingPurchaseItem> pendingItems)
    {
        return pendingItems
            .Where(item => item.remainingQuantity > 0 && item.itemCost > 0)
            .Select(item => new PurchaseEntry(
                item.itemId,
                item.itemName,
                item.remainingQuantity,
                item.itemCost,
                item.page,
                item.subPage,
                item.currencyItemId,
                item.index))
            .ToList();
    }

    public static int ResolvePurchaseQuantity(
        PurchaseEntry entry,
        int currentCurrencyCount,
        int reserveAmount)
    {
        if (entry.itemCost <= 0 || currentCurrencyCount < 0)
            return 0;

        var availableCurrency = Math.Max(0, currentCurrencyCount - reserveAmount);
        var maxByCurrency = availableCurrency / entry.itemCost;
        return Math.Min(entry.quantity, Math.Min(maxByCurrency, 99));
    }
}
