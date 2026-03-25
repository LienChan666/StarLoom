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

    private string itemSearch = string.Empty;

    public void Draw(Vector2 size)
    {
        if (!ImGui.BeginChild("##SearchPane", size, true))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##ItemSearch", P.Localization.Get("home.search.input_hint"), ref itemSearch, 128);

        if (P.ShopItems.IsLoading)
        {
            ImGui.TextDisabled(P.Localization.Get("home.search.loading_hint"));
            ImGui.EndChild();
            return;
        }

        var filteredItems = P.ShopItems.ShopItems
            .Where(static item => item.Discipline == ScripDiscipline.Crafting)
            .Where(item => string.IsNullOrWhiteSpace(itemSearch) || item.Name.Contains(itemSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .Take(MaxVisibleItems)
            .ToList();

        if (filteredItems.Count == 0)
        {
            ImGui.TextDisabled(P.Localization.Get("home.search.empty_hint"));
            ImGui.EndChild();
            return;
        }

        var configuredItemIds = GetConfiguredItemIds();
        ImGui.TextDisabled(P.Localization.Format("home.search.count", filteredItems.Count, MaxVisibleItems));

        var tableFlags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.Resizable
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.ScrollY;

        if (!ImGui.BeginTable("##SearchTable", 4, tableFlags, new Vector2(0f, -1f)))
        {
            ImGui.EndChild();
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
            ScripShopUiHelpers.DrawItemLabel(item);

            ImGui.TableSetColumnIndex(1);
            ScripShopUiHelpers.DrawCurrencyLabel(item);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(item.ItemCost.ToString());

            ImGui.TableSetColumnIndex(3);
            ImGui.BeginDisabled(alreadyAdded);
            if (ImGui.SmallButton("+##SearchAdd"))
                AddPurchaseItem(item);
            ImGui.EndDisabled();

            ImGui.PopID();
        }

        ImGui.EndTable();
        ImGui.EndChild();
    }

    private static void AddPurchaseItem(ScripShopItem item)
    {
        if (GetConfiguredItemIds().Contains(item.ItemId))
            return;

        P.ConfigEditor.AddPurchaseItem(item);
    }

    private static HashSet<uint> GetConfiguredItemIds()
        => C.ScripShopItems
            .Select(item => item.Item.ItemId)
            .ToHashSet();
}
