using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Tasks.Purchase;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;
using System.Numerics;

namespace StarLoom.Ui.Components.Home;

internal sealed class SearchPane
{
    private const int MaxVisibleItems = 100;

    private readonly PurchaseCatalog purchaseCatalog;
    private readonly ConfigStore configStore;
    private readonly UiText uiText;
    private string itemSearch = string.Empty;

    public SearchPane(PurchaseCatalog purchaseCatalog, ConfigStore configStore, UiText uiText)
    {
        this.purchaseCatalog = purchaseCatalog;
        this.configStore = configStore;
        this.uiText = uiText;
    }

    public void Draw(Vector2 size)
    {
        if (!ImGui.BeginChild("##SearchPane", size, true))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.TextUnformatted(uiText.Get("home.search.title"));
        ImGui.Separator();
        ImGui.TextUnformatted(uiText.Get("home.search.filter_label"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##ItemSearch", ref itemSearch, 128);

        if (purchaseCatalog.isLoading)
        {
            ImGui.TextDisabled(uiText.Get("home.search.loading_hint"));
            ImGui.EndChild();
            return;
        }

        var filteredItems = purchaseCatalog.itemsView
            .Where(static item => item.discipline == PurchaseDiscipline.Crafting)
            .Where(item => string.IsNullOrWhiteSpace(itemSearch) || item.itemName.Contains(itemSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.itemName, StringComparer.Ordinal)
            .Take(MaxVisibleItems)
            .ToList();

        if (filteredItems.Count == 0)
        {
            ImGui.TextDisabled(uiText.Get("home.search.empty_hint"));
            ImGui.EndChild();
            return;
        }

        var configuredItemIds = GetConfiguredItemIds();
        ImGui.TextDisabled(uiText.Format("home.search.count", filteredItems.Count, MaxVisibleItems));

        var tableFlags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.Resizable
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.ScrollY;

        GamePanelStyle.PushTableStyle();
        if (!ImGui.BeginTable("##SearchTable", 4, tableFlags, new Vector2(0f, -1f)))
        {
            GamePanelStyle.PopTableStyle();
            ImGui.EndChild();
            return;
        }

        ImGui.TableSetupColumn(uiText.Get("home.search.table.name"), ImGuiTableColumnFlags.WidthStretch, 0.50f);
        ImGui.TableSetupColumn(uiText.Get("home.search.table.currency"), ImGuiTableColumnFlags.WidthStretch, 0.22f);
        ImGui.TableSetupColumn(uiText.Get("home.search.table.cost"), ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn(uiText.Get("home.search.table.action"), ImGuiTableColumnFlags.WidthFixed, 64f);
        ImGui.TableHeadersRow();

        foreach (var item in filteredItems)
        {
            var alreadyAdded = configuredItemIds.Contains(item.itemId);
            ImGui.PushID((int)item.itemId);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(item.itemName);

            ImGui.TableSetColumnIndex(1);
            ScripShopUiHelpers.DrawCurrencyLabel(
                item.currencyName,
                ScripShopUiHelpers.ResolveCurrencyKind(item.currencySpecialId, item.currencyName),
                uiText.Get);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(item.itemCost.ToString());

            ImGui.TableSetColumnIndex(3);
            ImGui.BeginDisabled(alreadyAdded);
            if (ImGui.SmallButton("+##SearchAdd"))
                AddPurchaseItem(item);
            ImGui.EndDisabled();

            ImGui.PopID();
        }

        ImGui.EndTable();
        GamePanelStyle.PopTableStyle();
        ImGui.EndChild();
    }

    private void AddPurchaseItem(PurchaseCatalogItem item)
    {
        if (GetConfiguredItemIds().Contains(item.itemId))
            return;

        configStore.pluginConfig.scripShopItems.Add(new PurchaseItemConfig
        {
            itemId = item.itemId,
            itemName = item.itemName,
            index = item.index,
            targetCount = 1,
            scripCost = (int)item.itemCost,
            page = item.page,
            subPage = item.subPage,
            currencySpecialId = item.currencySpecialId,
            currencyItemId = item.currencyItemId,
            currencyName = item.currencyName,
        });
        configStore.Save();
    }

    private HashSet<uint> GetConfiguredItemIds()
    {
        return configStore.pluginConfig.scripShopItems
            .Select(item => item.itemId)
            .ToHashSet();
    }
}
