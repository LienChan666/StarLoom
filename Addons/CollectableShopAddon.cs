using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using static ECommons.GenericHelpers;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Starloom.Addons;

public sealed unsafe class CollectableShopAddon
{
    public bool IsReady => TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) && IsAddonReady(addon);

    public void SelectJob(uint id)
    {
        if (!TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) || !IsAddonReady(addon))
            return;

        var selectJob = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 14 },
            new() { Type = ValueType.UInt, UInt = id },
        };

        addon->FireCallback(2, selectJob);
    }

    public void SelectItemById(uint itemId)
    {
        var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
        if (item == null || item.Value.RowId == 0)
            return;

        SelectItem(item.Value.Name.ToString());
    }

    private void SelectItem(string itemName)
    {
        if (!TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) || !IsAddonReady(addon))
            return;

        var turnIn = new TurninWindowHelper(addon);
        var index = turnIn.GetItemIndexOf(itemName);
        if (index == -1)
        {
            Svc.Log.Error($"[CollectableShopAddon] Item '{itemName}' not found in current collectable tab");
            return;
        }

        var selectItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 12 },
            new() { Type = ValueType.UInt, UInt = (uint)index },
        };

        addon->FireCallback(2, selectItem);
    }

    public void SubmitItem()
    {
        if (!TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) || !IsAddonReady(addon))
            return;

        var submitItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 15 },
            new() { Type = ValueType.UInt, UInt = 0 },
        };

        addon->FireCallback(2, submitItem, true);
    }

    public void CloseWindow()
    {
        if (TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) && IsAddonReady(addon))
            addon->Close(true);
    }
}
