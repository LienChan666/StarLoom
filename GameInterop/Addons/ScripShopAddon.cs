using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Starloom.Data;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Starloom.GameInterop.Addons;

public sealed unsafe class ScripShopAddon
{
    public enum PurchaseDialogResult
    {
        Missing,
        MismatchedItem,
        Confirmed,
    }

    private int currentPage = -1;
    private int currentSubPage = -1;

    public bool IsReady => TryGetAddonByName<AtkUnitBase>("InclusionShop", out var addon) && IsAddonReady(addon);

    public void OpenShop()
    {
        if (!TryGetAddonByName("SelectIconString", out AtkUnitBase* addon))
            return;

        var openShop = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
        };

        addon->FireCallback(1, openShop);
    }

    public void SelectPage(int page)
    {
        currentPage = page;
        if (!TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
            return;

        var selectPage = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 12 },
            new() { Type = ValueType.UInt, UInt = (uint)page },
        };

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != (NodeType)1015 || node->NodeId != 7)
                continue;

            var compNode = node->GetAsAtkComponentNode();
            if (compNode == null || compNode->Component == null)
                continue;

            var dropDown = compNode->GetAsAtkComponentDropdownList();
            dropDown->SelectItem(page);
            addon->FireCallback(2, selectPage);
            break;
        }
    }

    public void SelectSubPage(int subPage)
    {
        currentSubPage = subPage;
        if (!TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
            return;

        var selectSubPage = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 13 },
            new() { Type = ValueType.UInt, UInt = (uint)subPage },
        };

        addon->FireCallback(2, selectSubPage);
    }

    public bool SelectItem(uint itemId, string itemName, int amount, List<ScripShopItem> shopItems)
    {
        if (!TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
            return false;

        var shopItem = shopItems.FirstOrDefault(x => x.ItemId == itemId && x.Page == currentPage && x.SubPage == currentSubPage);
        if (shopItem == null)
        {
            return false;
        }

        var selectItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 14 },
            new() { Type = ValueType.UInt, UInt = (uint)shopItem.Index },
            new() { Type = ValueType.UInt, UInt = (uint)amount },
        };

        addon->FireCallback(3, selectItem);
        return true;
    }

    public PurchaseDialogResult PurchaseItem(uint expectedItemId, string expectedItemName)
    {
        if (!TryGetAddonByName("ShopExchangeItemDialog", out AtkUnitBase* addon))
            return PurchaseDialogResult.Missing;

        var atkValueTexts = GetAtkValueTexts(addon, expectedItemId, out var hasExpectedItemId, out var atkValueDiagnostics);
        if (!hasExpectedItemId && !ContainsExpectedText(atkValueTexts, expectedItemName))
        {
            var visibleTexts = GetVisibleTexts(addon);
            addon->Close(true);
            return PurchaseDialogResult.MismatchedItem;
        }

        var purchaseItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
        };

        addon->FireCallback(1, purchaseItem);
        addon->Close(true);
        return PurchaseDialogResult.Confirmed;
    }

    private static List<string> GetAtkValueTexts(AtkUnitBase* addon, uint expectedItemId, out bool hasExpectedItemId, out List<string> diagnostics)
    {
        hasExpectedItemId = false;
        diagnostics = new List<string>();
        var texts = new List<string>();

        foreach (var value in addon->AtkValuesSpan)
        {
            switch (value.Type)
            {
                case ValueType.UInt:
                    diagnostics.Add($"UInt:{value.UInt}");
                    if (value.UInt == expectedItemId)
                        hasExpectedItemId = true;

                    break;
                case ValueType.Int:
                    diagnostics.Add($"Int:{value.Int}");
                    if (value.Int >= 0 && (uint)value.Int == expectedItemId)
                        hasExpectedItemId = true;

                    break;
                case ValueType.String:
                case ValueType.ManagedString:
                case ValueType.String8:
                    var text = value.GetValueAsString();
                    if (string.IsNullOrWhiteSpace(text))
                        break;

                    texts.Add(text);
                    diagnostics.Add($"{value.Type}:{text}");
                    break;
            }
        }

        return texts;
    }

    public void CloseShop()
    {
        if (TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
            addon->Close(true);
    }

    private static bool ContainsExpectedText(List<string> texts, string expectedItemName)
    {
        var expected = NormalizeLabel(expectedItemName);
        return texts.Any(text =>
        {
            var normalized = NormalizeLabel(text);
            return normalized.Length > 0 && (normalized == expected || normalized.Contains(expected) || expected.Contains(normalized));
        });
    }

    private static List<string> GetVisibleTexts(AtkUnitBase* addon)
    {
        var texts = new List<string>();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            CollectVisibleTexts(addon->UldManager.NodeList[i], texts);

        return texts.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct().ToList();
    }

    private static void CollectVisibleTexts(AtkResNode* node, List<string> texts)
    {
        for (var current = node; current != null; current = current->NextSiblingNode)
        {
            if (!current->IsVisible())
                continue;

            if (current->Type == NodeType.Text)
            {
                var textNode = current->GetAsAtkTextNode();
                var text = textNode == null ? string.Empty : textNode->NodeText.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    texts.Add(text);
            }

            if (current->ChildNode != null)
                CollectVisibleTexts(current->ChildNode, texts);
        }
    }

    private static string NormalizeLabel(string value)
        => string.Concat(value.Where(c => !char.IsWhiteSpace(c))).Trim();
}
