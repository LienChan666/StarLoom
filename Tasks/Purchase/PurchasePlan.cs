namespace StarLoom.Tasks.Purchase;

public static class PurchasePlan
{
    public static List<PurchaseEntry> BuildQueue(
        IEnumerable<PurchaseTarget> purchaseTargets,
        int currentScrips,
        int reserveAmount)
    {
        var spendableScrips = Math.Max(0, currentScrips - reserveAmount);
        var queue = new List<PurchaseEntry>();

        foreach (var purchaseTarget in purchaseTargets)
        {
            if (purchaseTarget.targetCount <= purchaseTarget.currentCount || purchaseTarget.scripCost <= 0)
                continue;

            var desiredQuantity = purchaseTarget.targetCount - purchaseTarget.currentCount;
            var affordableQuantity = spendableScrips / purchaseTarget.scripCost;
            var quantity = Math.Min(desiredQuantity, affordableQuantity);
            if (quantity <= 0)
                continue;

            var catalogItem = PurchaseCatalog.Resolve(purchaseTarget);
            queue.Add(new PurchaseEntry(
                purchaseTarget.itemId,
                purchaseTarget.itemName,
                quantity,
                purchaseTarget.scripCost,
                catalogItem.page,
                catalogItem.subPage));

            spendableScrips -= quantity * purchaseTarget.scripCost;
        }

        return queue;
    }
}
