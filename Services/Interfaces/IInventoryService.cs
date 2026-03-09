using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace Starloom.Services.Interfaces;

public interface IInventoryService
{
    void InvalidateTransientCaches();
    List<InventoryItem> GetCurrentInventoryItems();
    bool IsCollectableTurnInItem(uint itemId);
    int GetInventoryItemCount(uint itemId);
    int GetCollectableInventoryItemCount(uint itemId);
    int GetCurrencyItemCount(uint itemId);
    int GetFreeSlotCount();
    bool HasCollectableTurnIns();
    Item? GetItemRow(uint itemId);
}
