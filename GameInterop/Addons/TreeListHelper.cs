using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using System;

namespace Starloom.GameInterop.Addons;

public abstract unsafe class TreeListHelper
{
    protected readonly AtkUnitBase* Addon;
    protected readonly AtkComponentTreeList* TreeList;
    protected readonly StdVector<Pointer<AtkComponentTreeListItem>> Items;
    protected readonly string[] Labels;
    protected readonly int ItemCount;

    protected TreeListHelper(AtkUnitBase* addon)
    {
        Addon = addon;
        TreeList = FindTreeList(addon);
        ItemCount = TreeList == null ? 0 : (int)TreeList->Items.Count;
        Items = TreeList->Items;
        Labels = new string[ItemCount];
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
        if (TreeList == null)
            return;

        for (var i = 0; i < ItemCount; i++)
        {
            var item = Items[i].Value;
            Labels[i] = item != null ? ExtractLabel(item) : string.Empty;
        }
    }
}

public unsafe class TurninWindowHelper(AtkUnitBase* addon) : TreeListHelper(addon)
{
    protected override bool IsTargetNode(AtkResNode* node) => node->Type == (NodeType)1028 && node->NodeId == 28;

    protected override string ExtractLabel(AtkComponentTreeListItem* item)
    {
        var label = item->StringValues[0].Value;
        return SeString.Parse(label).TextValue;
    }

    public int GetItemIndexOf(string label)
    {
        var itemCount = 0;
        for (var i = 0; i < Labels.Length; i++)
        {
            var item = Items[i].Value;
            if (item == null)
                continue;

            var rawType = item->UIntValues.Count > 0 ? item->UIntValues[0] : 0;
            var itemType = (AtkComponentTreeListItemType)(rawType & 0xF);
            if (itemType == AtkComponentTreeListItemType.CollapsibleGroupHeader || itemType == AtkComponentTreeListItemType.GroupHeader)
                continue;

            if (Labels[i].Contains(label, StringComparison.OrdinalIgnoreCase))
                return itemCount;

            itemCount++;
        }

        return -1;
    }
}
