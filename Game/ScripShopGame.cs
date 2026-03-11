using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using StarLoom.Tasks.Purchase;
using static ECommons.GenericHelpers;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace StarLoom.Game;

public enum PurchaseDialogResult
{
    Missing,
    MismatchedItem,
    Confirmed,
}

public unsafe class ScripShopGame
{
    private int currentSelectedIndex = -1;

    public virtual bool IsReady()
    {
        return TryGetAddonByName<AtkUnitBase>("InclusionShop", out var addon) && IsAddonReady(addon);
    }

    public virtual bool OpenShop()
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectIconString", out var addon) || !IsAddonReady(addon))
            return false;

        var openShop = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
        };

        addon->FireCallback(1, openShop);
        return true;
    }

    public virtual void SelectPage(string page)
    {
        if (!int.TryParse(page, out var pageIndex))
            return;

        if (!TryGetAddonByName<AtkUnitBase>("InclusionShop", out var addon) || !IsAddonReady(addon))
            return;

        var selectPage = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 12 },
            new() { Type = ValueType.UInt, UInt = unchecked((uint)pageIndex) },
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
            dropDown->SelectItem(pageIndex);
            addon->FireCallback(2, selectPage);
            return;
        }
    }

    public virtual void SelectSubPage(string subPage)
    {
        if (!int.TryParse(subPage, out var subPageIndex))
            return;

        if (!TryGetAddonByName<AtkUnitBase>("InclusionShop", out var addon) || !IsAddonReady(addon))
            return;

        var selectSubPage = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 13 },
            new() { Type = ValueType.UInt, UInt = unchecked((uint)subPageIndex) },
        };

        addon->FireCallback(2, selectSubPage);
    }

    public virtual bool SelectItem(PurchaseEntry entry, int amount)
    {
        currentSelectedIndex = entry.index;
        return SelectItem(entry.itemId, entry.itemName, amount);
    }

    public virtual bool SelectItem(uint itemId, string itemName, int amount)
    {
        if (currentSelectedIndex < 0 || amount <= 0)
            return false;

        if (!TryGetAddonByName<AtkUnitBase>("InclusionShop", out var addon) || !IsAddonReady(addon))
            return false;

        var selectItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 14 },
            new() { Type = ValueType.UInt, UInt = unchecked((uint)currentSelectedIndex) },
            new() { Type = ValueType.UInt, UInt = unchecked((uint)amount) },
        };

        addon->FireCallback(3, selectItem);
        return true;
    }

    public virtual PurchaseDialogResult ConfirmPurchase(uint itemId, string itemName)
    {
        if (!TryGetAddonByName<AtkUnitBase>("ShopExchangeItemDialog", out var addon) || !IsAddonReady(addon))
            return PurchaseDialogResult.Missing;

        var texts = GetAtkValueTexts(addon, itemId, out var hasExpectedItemId);
        if (!hasExpectedItemId && !ContainsExpectedText(texts, itemName))
        {
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

    public virtual void CloseShop()
    {
        if (TryGetAddonByName<AtkUnitBase>("InclusionShop", out var addon) && IsAddonReady(addon))
            addon->Close(true);

        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var yesNoAddon) && IsAddonReady(&yesNoAddon->AtkUnitBase))
            new AddonMaster.SelectYesno((nint)yesNoAddon).No();

        currentSelectedIndex = -1;
    }

    private static List<string> GetAtkValueTexts(AtkUnitBase* addon, uint expectedItemId, out bool hasExpectedItemId)
    {
        hasExpectedItemId = false;
        var texts = new List<string>();

        foreach (var value in addon->AtkValuesSpan)
        {
            switch (value.Type)
            {
                case ValueType.UInt:
                    if (value.UInt == expectedItemId)
                        hasExpectedItemId = true;

                    break;
                case ValueType.Int:
                    if (value.Int >= 0 && unchecked((uint)value.Int) == expectedItemId)
                        hasExpectedItemId = true;

                    break;
                case ValueType.String:
                case ValueType.ManagedString:
                case ValueType.String8:
                    var text = value.GetValueAsString();
                    if (!string.IsNullOrWhiteSpace(text))
                        texts.Add(text);

                    break;
            }
        }

        return texts;
    }

    private static bool ContainsExpectedText(IEnumerable<string> texts, string expectedItemName)
    {
        var expected = NormalizeLabel(expectedItemName);
        return texts.Any(text =>
        {
            var normalized = NormalizeLabel(text);
            return normalized.Length > 0
                && (normalized == expected
                    || normalized.Contains(expected, StringComparison.Ordinal)
                    || expected.Contains(normalized, StringComparison.Ordinal));
        });
    }

    private static string NormalizeLabel(string value)
    {
        return string.Concat(value.Where(c => !char.IsWhiteSpace(c))).Trim();
    }
}
