using Dalamud.Game.Text.SeStringHandling;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Lumina.Excel.Sheets;
using static ECommons.GenericHelpers;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace StarLoom.Game;

public unsafe class CollectableShopGame
{
    public virtual bool IsReady()
    {
        return TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) && IsAddonReady(addon);
    }

    public virtual void SelectJob(uint jobId)
    {
        if (!TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) || !IsAddonReady(addon))
            return;

        var selectJob = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 14 },
            new() { Type = ValueType.UInt, UInt = jobId },
        };

        addon->FireCallback(2, selectJob);
    }

    public virtual void SelectItemById(uint itemId)
    {
        var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
        if (item == null || item.Value.RowId == 0)
            return;

        SelectItem(item.Value.Name.ToString());
    }

    public virtual void SubmitItem()
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

    public virtual bool TryDismissOvercapDialog()
    {
        if (!TryGetAddonByName<FFXIVClientStructs.FFXIV.Client.UI.AddonSelectYesno>("SelectYesno", out var addon)
            || !IsAddonReady(&addon->AtkUnitBase))
        {
            return false;
        }

        new AddonMaster.SelectYesno((nint)addon).No();
        return true;
    }

    public virtual void CloseWindow()
    {
        if (TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) && IsAddonReady(addon))
            addon->Close(true);
    }

    private static void SelectItem(string itemName)
    {
        if (!TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) || !IsAddonReady(addon))
            return;

        var turnIn = new TurnInWindowHelper(addon);
        var index = turnIn.GetItemIndexOf(itemName);
        if (index == -1)
            return;

        var selectItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 12 },
            new() { Type = ValueType.UInt, UInt = (uint)index },
        };

        addon->FireCallback(2, selectItem);
    }

    private abstract class TreeListHelper
    {
        protected readonly AtkComponentTreeList* treeList;
        protected readonly StdVector<Pointer<AtkComponentTreeListItem>> items;
        protected readonly string[] labels;
        protected readonly int itemCount;

        protected TreeListHelper(AtkUnitBase* addon)
        {
            treeList = FindTreeList(addon);
            itemCount = treeList == null ? 0 : (int)treeList->Items.Count;
            items = treeList == null ? default : treeList->Items;
            labels = new string[itemCount];
            PopulateLabels();
        }

        protected abstract bool IsTargetNode(AtkResNode* node);
        protected abstract string ExtractLabel(AtkComponentTreeListItem* item);

        private AtkComponentTreeList* FindTreeList(AtkUnitBase* addon)
        {
            for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node == null || !IsTargetNode(node))
                    continue;

                var compNode = node->GetAsAtkComponentNode();
                if (compNode == null || compNode->Component == null)
                    continue;

                return (AtkComponentTreeList*)compNode->Component;
            }

            return null;
        }

        private void PopulateLabels()
        {
            if (treeList == null)
                return;

            for (var i = 0; i < itemCount; i++)
            {
                var item = items[i].Value;
                labels[i] = item != null ? ExtractLabel(item) : string.Empty;
            }
        }
    }

    private sealed class TurnInWindowHelper(AtkUnitBase* addon) : TreeListHelper(addon)
    {
        protected override bool IsTargetNode(AtkResNode* node)
        {
            return node->Type == (NodeType)1028 && node->NodeId == 28;
        }

        protected override string ExtractLabel(AtkComponentTreeListItem* item)
        {
            var label = item->StringValues[0].Value;
            return SeString.Parse(label).TextValue;
        }

        public int GetItemIndexOf(string label)
        {
            var visibleItemIndex = 0;
            for (var i = 0; i < labels.Length; i++)
            {
                var item = items[i].Value;
                if (item == null)
                    continue;

                var rawType = item->UIntValues.Count > 0 ? item->UIntValues[0] : 0;
                var itemType = (AtkComponentTreeListItemType)(rawType & 0xF);
                if (itemType is AtkComponentTreeListItemType.CollapsibleGroupHeader or AtkComponentTreeListItemType.GroupHeader)
                    continue;

                if (labels[i].Contains(label, StringComparison.OrdinalIgnoreCase))
                    return visibleItemIndex;

                visibleItemIndex++;
            }

            return -1;
        }
    }
}
