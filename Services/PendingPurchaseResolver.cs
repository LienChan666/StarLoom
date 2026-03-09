using Starloom.Data;
using System.Collections.Generic;
using System.Linq;

namespace Starloom.Services;

public sealed class PendingPurchaseResolver
{
    private readonly Configuration config;
    private readonly InventoryService inventory;

    public PendingPurchaseResolver(Configuration config, InventoryService inventory)
    {
        this.config = config;
        this.inventory = inventory;
    }

    public IReadOnlyList<PendingPurchaseItem> Resolve()
        => config.ScripShopItems
            .Where(item => item.Item != null && item.Quantity > 0)
            .Where(item => item.Item!.Page < 3)
            .Select(item => CreatePendingItem(item))
            .Where(static item => item != null)
            .Cast<PendingPurchaseItem>()
            .ToList();

    public bool HasPending() => Resolve().Count > 0;

    private PendingPurchaseItem? CreatePendingItem(ItemToPurchase item)
    {
        var remaining = item.Quantity - inventory.GetInventoryItemCount(item.Item.ItemId);
        if (remaining <= 0)
            return null;

        return new PendingPurchaseItem(
            item.Item.ItemId,
            item.Name,
            remaining,
            (int)item.Item.ItemCost,
            item.Item.Page,
            item.Item.SubPage,
            item.Item.CurrencyItemId);
    }
}
