using StarLoom.Data;
using StarLoom.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace StarLoom.Workflows;

public sealed class PendingPurchaseResolver
{
    private readonly Configuration _config;
    private readonly IInventoryService _inventory;

    public PendingPurchaseResolver(Configuration config, IInventoryService inventory)
    {
        _config = config;
        _inventory = inventory;
    }

    public IReadOnlyList<PendingPurchaseItem> Resolve()
        => _config.ScripShopItems
            .Where(item => item.Item != null && item.Quantity > 0)
            .Where(item => item.Item!.Page < 3)
            .Select(item => CreatePendingItem(item))
            .Where(static item => item != null)
            .Cast<PendingPurchaseItem>()
            .ToList();

    private PendingPurchaseItem? CreatePendingItem(ItemToPurchase item)
    {
        var remaining = item.Quantity - _inventory.GetInventoryItemCount(item.Item.ItemId);
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
