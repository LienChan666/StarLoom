using Dalamud.Bindings.ImGui;
using Starloom.Data;
using Starloom.UI.Components.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class SearchPane
{
    private const int MaxVisibleItems = 100;

    private readonly Plugin _plugin;
    private List<ScripShopItem> _sortedCraftingItems = [];
    private List<ScripShopItem>? _sortedCraftingItemsSource;
    private int _sortedCraftingItemsSourceCount = -1;
    private string _itemSearch = string.Empty;

    public SearchPane(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##SearchPane", size, GamePanelStyle.AccentSoft);
        GamePanelStyle.DrawPanelHeader("搜索工票物品", "从制作工票目录中搜索目标并加入当前兑换队列。");

        GamePanelStyle.DrawSettingLabel("名称筛选");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##ItemSearch", ref _itemSearch, 128);
        ImGui.Separator();

        if (ScripShopItemManager.IsLoading)
        {
            GamePanelStyle.DrawHint("工票物品索引加载中...");
            return;
        }

        var allItems = ScripShopItemManager.ShopItems;
        if (allItems.Count == 0)
        {
            GamePanelStyle.DrawHint("未加载到工票物品索引，可在设置页的“物品索引”中点击“刷新物品列表”。");
            return;
        }

        RefreshSortedCraftingItems(allItems);
        var filteredItems = GetVisibleItems();
        var configuredItemIds = GetConfiguredItemIds();

        ImGui.TextDisabled($"显示 {filteredItems.Count} 条结果（最多 {MaxVisibleItems} 条）");
        ImGui.Separator();

        var tableFlags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.Resizable
            | ImGuiTableFlags.SizingStretchProp;

        if (!ImGui.BeginTable("##SearchTable", 4, tableFlags))
            return;

            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch, 0.50f);
            ImGui.TableSetupColumn("工票", ImGuiTableColumnFlags.WidthStretch, 0.22f);
            ImGui.TableSetupColumn("单价", ImGuiTableColumnFlags.WidthFixed, 70f);
            ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableHeadersRow();

        foreach (var item in filteredItems)
        {
            var alreadyAdded = configuredItemIds.Contains(item.ItemId);
            ImGui.PushID((int)item.ItemId);

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(item.Name);

            ImGui.TableSetColumnIndex(1);
            ScripShopUiHelpers.DrawCurrencyLabel(item);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(item.ItemCost.ToString());

            ImGui.TableSetColumnIndex(3);
            ImGui.BeginDisabled(alreadyAdded);
            if (ImGui.SmallButton("+"))
                AddPurchaseItem(item);
            ImGui.EndDisabled();

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void AddPurchaseItem(ScripShopItem item)
    {
        if (GetConfiguredItemIds().Contains(item.ItemId))
            return;

        _plugin.Config.ScripShopItems.Add(new ItemToPurchase
        {
            Item = item,
            Quantity = 1,
        });
        _plugin.Config.Save();
    }

    private List<ScripShopItem> GetVisibleItems()
    {
        var items = new List<ScripShopItem>(Math.Min(_sortedCraftingItems.Count, MaxVisibleItems));
        foreach (var item in _sortedCraftingItems)
        {
            if (!string.IsNullOrWhiteSpace(_itemSearch)
                && !item.Name.Contains(_itemSearch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(item);
            if (items.Count == MaxVisibleItems)
                break;
        }

        return items;
    }

    private HashSet<uint> GetConfiguredItemIds()
        => _plugin.Config.ScripShopItems
            .Select(item => item.Item.ItemId)
            .ToHashSet();

    private void RefreshSortedCraftingItems(List<ScripShopItem> allItems)
    {
        if (ReferenceEquals(_sortedCraftingItemsSource, allItems)
            && _sortedCraftingItemsSourceCount == allItems.Count)
        {
            return;
        }

        _sortedCraftingItemsSource = allItems;
        _sortedCraftingItemsSourceCount = allItems.Count;
        _sortedCraftingItems = allItems
            .Where(static item => item.Discipline == ScripDiscipline.Crafting)
            .ToList();
        _sortedCraftingItems.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
    }
}
