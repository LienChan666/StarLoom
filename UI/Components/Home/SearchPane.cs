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

    private List<ScripShopItem> sortedCraftingItems = [];
    private List<ScripShopItem>? sortedCraftingItemsSource;
    private int sortedCraftingItemsSourceCount = -1;
    private string itemSearch = string.Empty;

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##SearchPane", size, GamePanelStyle.BorderSubtle);
        GamePanelStyle.DrawPanelHeader(P.Localization.Get("home.search.title"), P.Localization.Get("home.search.description"));

        GamePanelStyle.DrawSettingLabel(P.Localization.Get("home.search.filter_label"));

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
        ImGui.InputText("##ItemSearch", ref itemSearch, 128);

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

        if (P.ShopItems.IsLoading)
        {
            GamePanelStyle.DrawHint(P.Localization.Get("home.search.loading_hint"));
            return;
        }

        var allItems = P.ShopItems.ShopItems.ToList();
        if (allItems.Count == 0)
        {
            GamePanelStyle.DrawHint(P.Localization.Get("home.search.empty_hint"));
            return;
        }

        RefreshSortedCraftingItems(allItems);
        var filteredItems = GetVisibleItems();
        var configuredItemIds = GetConfiguredItemIds();

        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextMuted);
        var countText = P.Localization.Format("home.search.count", filteredItems.Count, MaxVisibleItems);
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

        ImGui.TableSetupColumn(P.Localization.Get("home.search.table.name"), ImGuiTableColumnFlags.WidthStretch, 0.50f);
        ImGui.TableSetupColumn(P.Localization.Get("home.search.table.currency"), ImGuiTableColumnFlags.WidthStretch, 0.22f);
        ImGui.TableSetupColumn(P.Localization.Get("home.search.table.cost"), ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn(P.Localization.Get("home.search.table.action"), ImGuiTableColumnFlags.WidthFixed, 64f);
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

    private static void AddPurchaseItem(ScripShopItem item)
    {
        if (GetConfiguredItemIds().Contains(item.ItemId))
            return;

        C.ScripShopItems.Add(new ItemToPurchase
        {
            Item = item,
            Quantity = 1,
        });
        P.ConfigStore.Save();
    }

    private List<ScripShopItem> GetVisibleItems()
    {
        var items = new List<ScripShopItem>(Math.Min(sortedCraftingItems.Count, MaxVisibleItems));
        foreach (var item in sortedCraftingItems)
        {
            if (!string.IsNullOrWhiteSpace(itemSearch)
                && !item.Name.Contains(itemSearch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(item);
            if (items.Count == MaxVisibleItems)
                break;
        }

        return items;
    }

    private static HashSet<uint> GetConfiguredItemIds()
        => C.ScripShopItems
            .Select(item => item.Item.ItemId)
            .ToHashSet();

    private void RefreshSortedCraftingItems(List<ScripShopItem> allItems)
    {
        if (ReferenceEquals(sortedCraftingItemsSource, allItems)
            && sortedCraftingItemsSourceCount == allItems.Count)
        {
            return;
        }

        sortedCraftingItemsSource = allItems;
        sortedCraftingItemsSourceCount = allItems.Count;
        sortedCraftingItems = allItems
            .Where(static item => item.Discipline == ScripDiscipline.Crafting)
            .ToList();
        sortedCraftingItems.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
    }
}
