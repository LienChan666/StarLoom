using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using Starloom.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Starloom.Services;

public readonly record struct InventoryItem(uint BaseItemId, uint Quantity, bool IsCollectable);

public static unsafe class InventoryService
{
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMilliseconds(250);
    private static readonly InventoryType[] InventoryTypes =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    private static InventorySnapshot? _cachedSnapshot;
    private static DateTime _cachedSnapshotAt;
    private static HashSet<uint>? _collectableTurnInItemIds;

    private readonly record struct InventorySnapshot(List<InventoryItem> Items, int FreeSlotCount);

    public static void InvalidateTransientCaches()
    {
        _cachedSnapshot = null;
        _cachedSnapshotAt = DateTime.MinValue;
    }

    public static List<InventoryItem> GetCurrentInventoryItems()
        => GetCurrentSnapshot().Items;

    public static bool IsCollectableTurnInItem(uint itemId)
        => itemId != 0 && GetCollectableTurnInItemIds().Contains(itemId);

    public static int GetInventoryItemCount(uint itemId)
    {
        var manager = InventoryManager.Instance();
        if (manager == null)
            return 0;

        return (int)manager->GetInventoryItemCount(itemId);
    }

    public static int GetCollectableInventoryItemCount(uint itemId)
        => GetCurrentSnapshot().Items
            .Where(item => item.BaseItemId == itemId && item.IsCollectable)
            .Sum(item => (int)item.Quantity);

    public static int GetCurrencyItemCount(uint itemId)
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

    public static int GetFreeSlotCount()
        => GetCurrentSnapshot().FreeSlotCount;

    public static bool HasCollectableTurnIns()
        => GetCurrentSnapshot().Items.Any(item => item.IsCollectable && IsCollectableTurnInItem(item.BaseItemId));

    public static Item? GetItemRow(uint itemId)
        => Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);

    private static InventorySnapshot GetCurrentSnapshot()
    {
        if (_cachedSnapshot is { } cachedSnapshot
            && (DateTime.UtcNow - _cachedSnapshotAt) <= SnapshotLifetime)
        {
            return cachedSnapshot;
        }

        var snapshot = BuildSnapshot();
        _cachedSnapshot = snapshot;
        _cachedSnapshotAt = DateTime.UtcNow;
        return snapshot;
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

    private static HashSet<uint> GetCollectableTurnInItemIds()
    {
        if (_collectableTurnInItemIds != null)
            return _collectableTurnInItemIds;

        var shopSubSheet = Svc.Data.GetSubrowExcelSheet<CollectablesShopItem>();
        _collectableTurnInItemIds = shopSubSheet == null
            ? []
            : shopSubSheet.SelectMany(sheet => sheet).Select(row => row.Item.RowId).ToHashSet();

        return _collectableTurnInItemIds;
    }
}

public sealed class InventoryServiceAdapter : IInventoryService
{
    public void InvalidateTransientCaches()
        => InventoryService.InvalidateTransientCaches();

    public List<InventoryItem> GetCurrentInventoryItems()
        => InventoryService.GetCurrentInventoryItems();

    public bool IsCollectableTurnInItem(uint itemId)
        => InventoryService.IsCollectableTurnInItem(itemId);

    public int GetInventoryItemCount(uint itemId)
        => InventoryService.GetInventoryItemCount(itemId);

    public int GetCollectableInventoryItemCount(uint itemId)
        => InventoryService.GetCollectableInventoryItemCount(itemId);

    public int GetCurrencyItemCount(uint itemId)
        => InventoryService.GetCurrencyItemCount(itemId);

    public int GetFreeSlotCount()
        => InventoryService.GetFreeSlotCount();

    public bool HasCollectableTurnIns()
        => InventoryService.HasCollectableTurnIns();

    public Item? GetItemRow(uint itemId)
        => InventoryService.GetItemRow(itemId);
}
