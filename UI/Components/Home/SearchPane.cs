using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace StarLoom.UI.Components.Home;

internal sealed class SearchPane
{
    private const int MaxVisibleItems = 100;

    private readonly IPluginUiFacade _ui;
    private List<ScripShopItem> _sortedCraftingItems = [];
    private List<ScripShopItem>? _sortedCraftingItemsSource;
    private int _sortedCraftingItemsSourceCount = -1;
    private string _itemSearch = string.Empty;

    public SearchPane(IPluginUiFacade ui)
    {
        _ui = ui;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##SearchPane", size, GamePanelStyle.BorderSubtle);
        GamePanelStyle.DrawPanelHeader(_ui.GetText("home.search.title"), _ui.GetText("home.search.description"));

        GamePanelStyle.DrawSettingLabel(_ui.GetText("home.search.filter_label"));

        ImGui.PushStyleColor(ImGuiCol.FrameBg, GamePanelStyle.Layer0);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, GamePanelStyle.Layer0);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, GamePanelStyle.Layer0);
        ImGui.PushStyleColor(ImGuiCol.Border, GamePanelStyle.BorderSubtle);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);

        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextSecond);
        ImGui.TextUnformatted(">");
        ImGui.PopStyleColor();
        ImGui.SameLine();

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##ItemSearch", ref _itemSearch, 128);

        if (ImGui.IsItemActive())
        {
            ImGui.GetWindowDrawList().AddRect(
                ImGui.GetItemRectMin(),
                ImGui.GetItemRectMax(),
                ImGui.GetColorU32(GamePanelStyle.Accent),
                6f,
                ImDrawFlags.RoundCornersAll,
                1f);
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);

        GamePanelStyle.DrawGradientSeparator();

        if (_ui.IsCatalogLoading)
        {
            GamePanelStyle.DrawHint(_ui.GetText("home.search.loading_hint"));
            return;
        }

        var allItems = _ui.ShopItems.ToList();
        if (allItems.Count == 0)
        {
            GamePanelStyle.DrawHint(_ui.GetText("home.search.empty_hint"));
            return;
        }

        RefreshSortedCraftingItems(allItems);
        var filteredItems = GetVisibleItems();
        var configuredItemIds = GetConfiguredItemIds();

        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextMuted);
        var countText = _ui.GetText("home.search.count", filteredItems.Count, MaxVisibleItems);
        var countWidth = ImGui.CalcTextSize(countText).X;
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - countWidth + ImGui.GetCursorPosX());
        ImGui.TextUnformatted(countText);
        ImGui.PopStyleColor();

        var tableFlags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.Resizable
            | ImGuiTableFlags.SizingStretchProp;

        GamePanelStyle.PushTableStyle();

        if (!ImGui.BeginTable("##SearchTable", 4, tableFlags))
        {
            GamePanelStyle.PopTableStyle();
            return;
        }

        ImGui.TableSetupColumn(_ui.GetText("home.search.table.name"), ImGuiTableColumnFlags.WidthStretch, 0.50f);
        ImGui.TableSetupColumn(_ui.GetText("home.search.table.currency"), ImGuiTableColumnFlags.WidthStretch, 0.22f);
        ImGui.TableSetupColumn(_ui.GetText("home.search.table.cost"), ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn(_ui.GetText("home.search.table.action"), ImGuiTableColumnFlags.WidthFixed, 64f);
        ImGui.TableHeadersRow();

        foreach (var item in filteredItems)
        {
            var alreadyAdded = configuredItemIds.Contains(item.ItemId);
            ImGui.PushID((int)item.ItemId);

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(item.Name);

            ImGui.TableSetColumnIndex(1);
            ScripShopUiHelpers.DrawCurrencyLabel(_ui, item);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(item.ItemCost.ToString());

            ImGui.TableSetColumnIndex(3);
            ImGui.BeginDisabled(alreadyAdded);
            ImGui.PushStyleColor(ImGuiCol.Button, GamePanelStyle.Tint(GamePanelStyle.AccentSoft, 0.6f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, GamePanelStyle.Tint(GamePanelStyle.Accent, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, GamePanelStyle.Tint(GamePanelStyle.Accent, 0.9f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
            if (ImGui.SmallButton("+##SearchAdd"))
                AddPurchaseItem(item);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
            ImGui.EndDisabled();

            ImGui.PopID();
        }

        ImGui.EndTable();
        GamePanelStyle.PopTableStyle();
    }

    private void AddPurchaseItem(ScripShopItem item)
    {
        if (GetConfiguredItemIds().Contains(item.ItemId))
            return;

        _ui.Config.ScripShopItems.Add(new ItemToPurchase
        {
            Item = item,
            Quantity = 1,
        });
        _ui.SaveConfig();
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
        => _ui.Config.ScripShopItems
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

