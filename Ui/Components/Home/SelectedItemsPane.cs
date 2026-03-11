using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;
using System.Numerics;

namespace StarLoom.Ui.Components.Home;

internal sealed class SelectedItemsPane
{
    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public SelectedItemsPane(ConfigStore configStore, UiText uiText)
    {
        this.configStore = configStore;
        this.uiText = uiText;
    }

    public void Draw(Vector2 size)
    {
        if (!ImGui.BeginChild("##SelectedPane", size, true))
        {
            ImGui.EndChild();
            return;
        }

        var configuredItems = configStore.pluginConfig.scripShopItems;
        var totalQuantity = configuredItems.Sum(item => item.targetCount);
        ImGui.TextUnformatted(uiText.Get("home.selected.title"));
        ImGui.SameLine();
        ImGui.TextDisabled($"{configuredItems.Count} / {totalQuantity}");
        ImGui.Separator();

        if (configuredItems.Count == 0)
        {
            ImGui.TextDisabled(uiText.Get("home.selected.empty_hint"));
            ImGui.EndChild();
            return;
        }

        int? removeIndex = null;
        int? moveUpIndex = null;
        int? moveDownIndex = null;

        var tableFlags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.Resizable
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.ScrollY;

        GamePanelStyle.PushTableStyle();
        if (ImGui.BeginTable("##SelectedTable", 6, tableFlags, new Vector2(0f, -1f)))
        {
            ImGui.TableSetupColumn(uiText.Get("home.selected.table.name"), ImGuiTableColumnFlags.WidthStretch, 0.34f);
            ImGui.TableSetupColumn(uiText.Get("home.selected.table.currency"), ImGuiTableColumnFlags.WidthStretch, 0.18f);
            ImGui.TableSetupColumn(uiText.Get("home.selected.table.cost"), ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableSetupColumn(uiText.Get("home.selected.table.quantity"), ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableSetupColumn(uiText.Get("home.selected.table.order"), ImGuiTableColumnFlags.WidthFixed, 78f);
            ImGui.TableSetupColumn(uiText.Get("home.selected.table.action"), ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableHeadersRow();

            for (var index = 0; index < configuredItems.Count; index++)
            {
                var item = configuredItems[index];
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
                ImGui.TextUnformatted(item.scripCost.ToString());

                ImGui.TableSetColumnIndex(3);
                var quantity = item.targetCount;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputInt("##Quantity", ref quantity, 0, 0))
                    item.targetCount = Math.Max(1, quantity);

                if (ImGui.IsItemDeactivatedAfterEdit())
                    configStore.Save();

                ImGui.TableSetColumnIndex(4);
                ImGui.BeginDisabled(index == 0);
                if (ImGui.SmallButton("^##MoveUp"))
                    moveUpIndex = index;
                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(index == configuredItems.Count - 1);
                if (ImGui.SmallButton("v##MoveDown"))
                    moveDownIndex = index;
                ImGui.EndDisabled();

                ImGui.TableSetColumnIndex(5);
                if (ImGui.SmallButton("x##Remove"))
                    removeIndex = index;

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        GamePanelStyle.PopTableStyle();
        ImGui.EndChild();

        if (removeIndex.HasValue)
        {
            configuredItems.RemoveAt(removeIndex.Value);
            configStore.Save();
        }

        if (moveUpIndex.HasValue)
        {
            ApplyOrdering(SelectedItemsOrdering.MoveUp(configuredItems, moveUpIndex.Value));
        }

        if (moveDownIndex.HasValue)
        {
            ApplyOrdering(SelectedItemsOrdering.MoveDown(configuredItems, moveDownIndex.Value));
        }
    }

    private void ApplyOrdering(IReadOnlyList<PurchaseItemConfig> orderedItems)
    {
        configStore.pluginConfig.scripShopItems = orderedItems.ToList();
        configStore.Save();
    }
}

internal static class SelectedItemsOrdering
{
    internal static IReadOnlyList<T> MoveUp<T>(IReadOnlyList<T> items, int index)
    {
        if (index <= 0 || index >= items.Count)
            return items.ToList();

        var reordered = items.ToList();
        (reordered[index - 1], reordered[index]) = (reordered[index], reordered[index - 1]);
        return reordered;
    }

    internal static IReadOnlyList<T> MoveDown<T>(IReadOnlyList<T> items, int index)
    {
        if (index < 0 || index >= items.Count - 1)
            return items.ToList();

        var reordered = items.ToList();
        (reordered[index], reordered[index + 1]) = (reordered[index + 1], reordered[index]);
        return reordered;
    }
}
