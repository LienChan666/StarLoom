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
        if (!ImGui.BeginChild("##SelectedPane", size, true))
        {
            ImGui.EndChild();
            return;
        }

        var totalQuantity = C.ScripShopItems.Sum(item => item.Quantity);
        ImGui.TextUnformatted(P.Localization.Get("home.selected.title"));
        ImGui.SameLine();
        ImGui.TextDisabled($"{C.ScripShopItems.Count} / {totalQuantity}");
        ImGui.Separator();

        if (C.ScripShopItems.Count == 0)
        {
            ImGui.TextDisabled(P.Localization.Get("home.selected.empty_hint"));
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

        if (ImGui.BeginTable("##SelectedTable", 7, tableFlags, new Vector2(0f, -1f)))
        {
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.name"), ImGuiTableColumnFlags.WidthStretch, 0.30f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.currency"), ImGuiTableColumnFlags.WidthStretch, 0.18f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.cost"), ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableSetupColumn(P.Localization.Get("home.selected.table.owned"), ImGuiTableColumnFlags.WidthFixed, 72f);
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
                ScripShopUiHelpers.DrawItemLabel(item.Item);

                ImGui.TableSetColumnIndex(1);
                ScripShopUiHelpers.DrawCurrencyLabel(item.Item);

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(item.Item.ItemCost.ToString());

                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(P.Inventory.GetInventoryItemCount(item.Item.ItemId).ToString());

                ImGui.TableSetColumnIndex(4);
                var quantity = item.Quantity;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputInt("##Quantity", ref quantity, 0, 0))
                    quantity = Math.Max(1, quantity);

                if (ImGui.IsItemDeactivatedAfterEdit())
                    P.ConfigEditor.SetPurchaseItemQuantity(index, quantity);

                ImGui.TableSetColumnIndex(5);
                ImGui.BeginDisabled(index == 0);
                if (ImGui.SmallButton("^##MoveUp"))
                    moveUpIndex = index;
                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(index == C.ScripShopItems.Count - 1);
                if (ImGui.SmallButton("v##MoveDown"))
                    moveDownIndex = index;
                ImGui.EndDisabled();

                ImGui.TableSetColumnIndex(6);
                if (ImGui.SmallButton("x##Remove"))
                    removeIndex = index;

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();

        if (removeIndex.HasValue)
            P.ConfigEditor.RemovePurchaseItemAt(removeIndex.Value);

        if (moveUpIndex.HasValue)
            P.ConfigEditor.MovePurchaseItem(moveUpIndex.Value, moveUpIndex.Value - 1);

        if (moveDownIndex.HasValue)
            P.ConfigEditor.MovePurchaseItem(moveDownIndex.Value, moveDownIndex.Value + 1);
    }
}
