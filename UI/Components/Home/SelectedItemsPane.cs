using Dalamud.Bindings.ImGui;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System;
using System.Linq;
using System.Numerics;

namespace StarLoom.UI.Components.Home;

internal sealed class SelectedItemsPane
{
    private readonly IPluginUiFacade _ui;

    public SelectedItemsPane(IPluginUiFacade ui)
    {
        _ui = ui;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##SelectedPane", size, GamePanelStyle.BorderSubtle);

        var totalQuantity = _ui.Config.ScripShopItems.Sum(item => item.Quantity);
        GamePanelStyle.DrawPanelHeader(
            _ui.GetText("home.selected.title"),
            _ui.GetText("home.selected.description", _ui.Config.ScripShopItems.Count, totalQuantity));

        if (_ui.Config.ScripShopItems.Count == 0)
        {
            GamePanelStyle.DrawHint(_ui.GetText("home.selected.empty_hint"));
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
            ImGui.TableSetupColumn(_ui.GetText("home.selected.table.name"), ImGuiTableColumnFlags.WidthStretch, 0.34f);
            ImGui.TableSetupColumn(_ui.GetText("home.selected.table.currency"), ImGuiTableColumnFlags.WidthStretch, 0.18f);
            ImGui.TableSetupColumn(_ui.GetText("home.selected.table.cost"), ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableSetupColumn(_ui.GetText("home.selected.table.quantity"), ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableSetupColumn(_ui.GetText("home.selected.table.order"), ImGuiTableColumnFlags.WidthFixed, 78f);
            ImGui.TableSetupColumn(_ui.GetText("home.selected.table.action"), ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableHeadersRow();

            for (var index = 0; index < _ui.Config.ScripShopItems.Count; index++)
            {
                var item = _ui.Config.ScripShopItems[index];
                ImGui.PushID((int)item.Item.ItemId);

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(item.Name);

                ImGui.TableSetColumnIndex(1);
            ScripShopUiHelpers.DrawCurrencyLabel(_ui, item.Item);

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(item.Item.ItemCost.ToString());

                ImGui.TableSetColumnIndex(3);
                var quantity = item.Quantity;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt("##Quantity", ref quantity, 0, 0))
                    item.Quantity = Math.Max(1, quantity);

                if (ImGui.IsItemDeactivatedAfterEdit())
                    _ui.SaveConfig();

                ImGui.TableSetColumnIndex(4);
                ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextSecond);
                ImGui.BeginDisabled(index == 0);
                if (ImGui.SmallButton("↑##MoveUp"))
                    moveUpIndex = index;
                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(index == _ui.Config.ScripShopItems.Count - 1);
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
            _ui.Config.ScripShopItems.RemoveAt(removeIndex.Value);
            _ui.SaveConfig();
        }

        if (moveUpIndex.HasValue)
        {
            SwapItems(moveUpIndex.Value, moveUpIndex.Value - 1);
            _ui.SaveConfig();
        }

        if (moveDownIndex.HasValue)
        {
            SwapItems(moveDownIndex.Value, moveDownIndex.Value + 1);
            _ui.SaveConfig();
        }
    }

    private void SwapItems(int firstIndex, int secondIndex)
    {
        var list = _ui.Config.ScripShopItems;
        (list[firstIndex], list[secondIndex]) = (list[secondIndex], list[firstIndex]);
    }
}

