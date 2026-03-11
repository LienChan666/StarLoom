namespace StarLoom.Game;

public sealed class InventoryGame
{
    public List<InventoryItemView> GetInventoryItems()
    {
        return [];
    }

    public int GetItemCount(uint itemId)
    {
        return 0;
    }

    public int GetCurrencyCount(uint itemId)
    {
        return 0;
    }

    public static List<CollectableGroup> GroupCollectables(IEnumerable<InventoryItemView> items)
    {
        return items
            .Where(item => item.isCollectable)
            .GroupBy(item => item.itemId)
            .Select(group => new CollectableGroup(group.Key, group.Sum(item => item.quantity)))
            .ToList();
    }
}

public readonly record struct InventoryItemView(uint itemId, bool isCollectable, int quantity);

public readonly record struct CollectableGroup(uint itemId, int quantity);
