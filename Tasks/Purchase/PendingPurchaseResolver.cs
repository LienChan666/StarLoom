using StarLoom.Config;
using StarLoom.Game;

namespace StarLoom.Tasks.Purchase;

public sealed class PendingPurchaseResolver
{
    private readonly PluginConfig pluginConfig;
    private readonly InventoryGame inventoryGame;

    public PendingPurchaseResolver(PluginConfig pluginConfig, InventoryGame inventoryGame)
    {
        this.pluginConfig = pluginConfig;
        this.inventoryGame = inventoryGame;
    }

    public IReadOnlyList<PendingPurchaseItem> ResolvePendingTargets()
    {
        return pluginConfig.scripShopItems
            .Where(item => item.itemId > 0 && item.targetCount > 0 && item.scripCost > 0)
            .Select(CreatePendingItem)
            .Where(static item => item is not null)
            .Select(static item => item!.Value)
            .ToList();
    }

    public bool HasPending()
    {
        return ResolvePendingTargets().Count > 0;
    }

    private PendingPurchaseItem? CreatePendingItem(PurchaseItemConfig configuredItem)
    {
        var remainingQuantity = configuredItem.targetCount - inventoryGame.GetItemCount(configuredItem.itemId);
        if (remainingQuantity <= 0)
            return null;

        return new PendingPurchaseItem(
            configuredItem.itemId,
            configuredItem.itemName,
            remainingQuantity,
            configuredItem.scripCost,
            configuredItem.page,
            configuredItem.subPage,
            configuredItem.currencyItemId,
            configuredItem.index);
    }
}
