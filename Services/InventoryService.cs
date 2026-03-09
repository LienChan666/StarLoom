using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Starloom.Services;

public readonly record struct InventoryItem(uint BaseItemId, uint Quantity, bool IsCollectable);

public sealed unsafe class InventoryService
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
    private DateTime cachedSnapshotAt;
    private HashSet<uint>? collectableTurnInItemIds;

    private readonly record struct InventorySnapshot(List<InventoryItem> Items, int FreeSlotCount);

    public void InvalidateTransientCaches()
    {
        cachedSnapshot = null;
        cachedSnapshotAt = DateTime.MinValue;
    }

    public List<InventoryItem> GetCurrentInventoryItems()
        => GetCurrentSnapshot().Items;

    public bool IsCollectableTurnInItem(uint itemId)
        => itemId != 0 && GetCollectableTurnInItemIds().Contains(itemId);

    public int GetInventoryItemCount(uint itemId)
    {
        var manager = InventoryManager.Instance();
        if (manager == null)
            return 0;

        return (int)manager->GetInventoryItemCount(itemId);
    }

    public int GetCollectableInventoryItemCount(uint itemId)
        => GetCurrentSnapshot().Items
            .Where(item => item.BaseItemId == itemId && item.IsCollectable)
            .Sum(item => (int)item.Quantity);

    public int GetCurrencyItemCount(uint itemId)
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

    public int GetFreeSlotCount()
        => GetCurrentSnapshot().FreeSlotCount;

    public bool HasCollectableTurnIns()
        => GetCurrentSnapshot().Items.Any(item => item.IsCollectable && IsCollectableTurnInItem(item.BaseItemId));

    public Item? GetItemRow(uint itemId)
        => Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);

    private InventorySnapshot GetCurrentSnapshot()
    {
        if (cachedSnapshot is { } snapshot
            && (DateTime.UtcNow - cachedSnapshotAt) <= SnapshotLifetime)
        {
            return snapshot;
        }

        var newSnapshot = BuildSnapshot();
        cachedSnapshot = newSnapshot;
        cachedSnapshotAt = DateTime.UtcNow;
        return newSnapshot;
    }

    private static InventorySnapshot BuildSnapshot()
    {
        var items = new List<InventoryItem>(140);
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
                items.Add(new InventoryItem(slot->ItemId, unchecked((uint)slot->Quantity), isCollectable));
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
