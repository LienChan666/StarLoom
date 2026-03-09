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

        if (ImGui.BeginTable("##SelectedTable", 6, tableFlags, new Vector2(0f, -1f)))
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
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputInt("##Quantity", ref quantity, 0, 0))
                    item.Quantity = Math.Max(1, quantity);

                if (ImGui.IsItemDeactivatedAfterEdit())
                    P.ConfigStore.Save();

                ImGui.TableSetColumnIndex(4);
                ImGui.BeginDisabled(index == 0);
                if (ImGui.SmallButton("^##MoveUp"))
                    moveUpIndex = index;
                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(index == C.ScripShopItems.Count - 1);
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

        ImGui.EndChild();

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
