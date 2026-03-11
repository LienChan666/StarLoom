using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace StarLoom.Game;

public unsafe class InventoryGame
{
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMilliseconds(250);
    private static readonly InventoryType[] InventoryTypes =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    private InventorySnapshot? cachedSnapshot;
    private DateTime cachedSnapshotAt = DateTime.MinValue;
    private HashSet<uint>? collectableTurnInItemIds;

    protected readonly record struct InventorySnapshot(List<InventoryItemView> Items, int FreeSlotCount);

    public virtual void InvalidateTransientCaches()
    {
        cachedSnapshot = null;
        cachedSnapshotAt = DateTime.MinValue;
    }

    public virtual List<InventoryItemView> GetInventoryItems()
    {
        return GetCurrentSnapshot().Items;
    }

    public virtual bool IsCollectableTurnInItem(uint itemId)
    {
        return itemId != 0 && GetCollectableTurnInItemIds().Contains(itemId);
    }

    public virtual string GetItemName(uint itemId)
    {
        var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
        return item?.Name.ToString() ?? string.Empty;
    }

    public virtual int GetItemCount(uint itemId)
    {
        var manager = InventoryManager.Instance();
        if (manager == null)
            return 0;

        return (int)manager->GetInventoryItemCount(itemId);
    }

    public virtual int GetCollectableInventoryItemCount(uint itemId)
    {
        return GetCurrentSnapshot().Items
            .Where(item => item.itemId == itemId && item.isCollectable)
            .Sum(item => item.quantity);
    }

    public virtual int GetCurrencyCount(uint itemId)
    {
        if (itemId == 0)
            return -1;

        var manager = InventoryManager.Instance();
        if (manager == null)
            return -1;

        var tomestoneCount = (int)manager->GetTomestoneCount(itemId);
        var inventoryCount = (int)manager->GetInventoryItemCount(itemId, false, false, false);
        return Math.Max(tomestoneCount, inventoryCount);
    }

    public virtual int GetFreeSlotCount()
    {
        return GetCurrentSnapshot().FreeSlotCount;
    }

    public virtual bool HasCollectableTurnIns()
    {
        return GetCurrentSnapshot().Items.Any(item => item.isCollectable && IsCollectableTurnInItem(item.itemId));
    }

    public static List<CollectableGroup> GroupCollectables(IEnumerable<InventoryItemView> items)
    {
        return items
            .Where(item => item.isCollectable)
            .GroupBy(item => item.itemId)
            .Select(group => new CollectableGroup(group.Key, group.Sum(item => item.quantity)))
            .ToList();
    }

    private InventorySnapshot GetCurrentSnapshot()
    {
        if (cachedSnapshot is { } snapshot
            && (DateTime.UtcNow - cachedSnapshotAt) <= SnapshotLifetime)
        {
            return snapshot;
        }

        cachedSnapshot = BuildSnapshot();
        cachedSnapshotAt = DateTime.UtcNow;
        return cachedSnapshot.Value;
    }

    private InventorySnapshot BuildSnapshot()
    {
        var items = new List<InventoryItemView>(140);
        var freeSlotCount = 0;
        var manager = InventoryManager.Instance();
        if (manager == null)
            return new InventorySnapshot(items, freeSlotCount);

        foreach (var inventoryType in InventoryTypes)
        {
            var container = manager->GetInventoryContainer(inventoryType);
            if (container == null || !container->IsLoaded)
                continue;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null)
                    continue;

                if (slot->ItemId == 0)
                {
                    freeSlotCount++;
                    continue;
                }

                var isCollectable = (slot->Flags & FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags.Collectable) != 0;
                items.Add(new InventoryItemView(
                    slot->ItemId,
                    isCollectable,
                    unchecked((int)slot->Quantity)));
            }
        }

        return new InventorySnapshot(items, freeSlotCount);
    }

    private HashSet<uint> GetCollectableTurnInItemIds()
    {
        if (collectableTurnInItemIds != null)
            return collectableTurnInItemIds;

        var shopSubSheet = Svc.Data.GetSubrowExcelSheet<CollectablesShopItem>();
        collectableTurnInItemIds = shopSubSheet == null
            ? []
            : shopSubSheet.SelectMany(sheet => sheet).Select(row => row.Item.RowId).ToHashSet();

        return collectableTurnInItemIds;
    }
}

public readonly record struct InventoryItemView(uint itemId, bool isCollectable, int quantity, string itemName = "");

public readonly record struct CollectableGroup(uint itemId, int quantity);
