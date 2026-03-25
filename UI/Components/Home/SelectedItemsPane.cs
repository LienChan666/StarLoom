using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System;
using System.Linq;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class SelectedItemsPane
{
    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##SelectedPane", size, GamePanelStyle.BorderSubtle);

        var totalQuantity = C.ScripShopItems.Sum(item => item.Quantity);
        GamePanelStyle.DrawPanelHeader(
            P.Localization.Get("home.selected.title"),
            P.Localization.Format("home.selected.description", C.ScripShopItems.Count, totalQuantity));

        if (C.ScripShopItems.Count == 0)
        {
            GamePanelStyle.DrawHint(P.Localization.Get("home.selected.empty_hint"));
            return;
        }

        int? removeIndex = null;
        int? moveUpIndex = null;
        int? moveDownIndex = null;

        var tableFlags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.Resizable
            | ImGuiTableFlags.SizingStretchProp;

        GamePanelStyle.PushTableStyle();

        if (ImGui.BeginTable("##SelectedTable", 6, tableFlags))
        {
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.name"), ImGuiTableColumnFlags.WidthStretch, 0.34f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.currency"), ImGuiTableColumnFlags.WidthStretch, 0.18f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.cost"), ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.quantity"), ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.order"), ImGuiTableColumnFlags.WidthFixed, 78f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.action"), ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableHeadersRow();

            for (var index = 0; index < C.ScripShopItems.Count; index++)
            {
                var item = C.ScripShopItems[index];
                ImGui.PushID((int)item.Item.ItemId);

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(item.Name);

                ImGui.TableSetColumnIndex(1);
                ScripShopUiHelpers.DrawCurrencyLabel(item.Item);

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(item.Item.ItemCost.ToString());

                ImGui.TableSetColumnIndex(3);
                var quantity = item.Quantity;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt("##Quantity", ref quantity, 0, 0))
                    item.Quantity = Math.Max(1, quantity);

                if (ImGui.IsItemDeactivatedAfterEdit())
                    P.ConfigStore.Save();

                ImGui.TableSetColumnIndex(4);
                ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextSecond);
                ImGui.BeginDisabled(index == 0);
                if (ImGui.SmallButton("↑##MoveUp"))
                    moveUpIndex = index;
                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(index == C.ScripShopItems.Count - 1);
                if (ImGui.SmallButton("↓##MoveDown"))
                    moveDownIndex = index;
                ImGui.EndDisabled();
                ImGui.PopStyleColor();

                ImGui.TableSetColumnIndex(5);
                ImGui.PushStyleColor(ImGuiCol.Button, GamePanelStyle.Tint(GamePanelStyle.Danger, 0.3f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, GamePanelStyle.Tint(GamePanelStyle.Danger, 0.5f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, GamePanelStyle.Tint(GamePanelStyle.Danger, 0.7f));
                if (ImGui.SmallButton("×##Remove"))
                    removeIndex = index;
                ImGui.PopStyleColor(3);

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        GamePanelStyle.PopTableStyle();

        if (removeIndex.HasValue)
        {
            C.ScripShopItems.RemoveAt(removeIndex.Value);
            P.ConfigStore.Save();
        }

        if (moveUpIndex.HasValue)
        {
            SwapItems(moveUpIndex.Value, moveUpIndex.Value - 1);
            P.ConfigStore.Save();
        }

        if (moveDownIndex.HasValue)
        {
            SwapItems(moveDownIndex.Value, moveDownIndex.Value + 1);
            P.ConfigStore.Save();
        }
    }

    private static void SwapItems(int firstIndex, int secondIndex)
    {
        var list = C.ScripShopItems;
        (list[firstIndex], list[secondIndex]) = (list[secondIndex], list[firstIndex]);
    }
}
