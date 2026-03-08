using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System;
using System.Linq;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class SelectedItemsPane
{
    private readonly Plugin _plugin;

    public SelectedItemsPane(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##SelectedPane", size, GamePanelStyle.AccentSoft);

        var totalQuantity = _plugin.Config.ScripShopItems.Sum(item => item.Quantity);
        GamePanelStyle.DrawPanelHeader("已选兑换列表", $"共 {_plugin.Config.ScripShopItems.Count} 项，目标数量合计 {totalQuantity}。");

        if (_plugin.Config.ScripShopItems.Count == 0)
        {
            GamePanelStyle.DrawHint("当前还没有配置任何兑换物品，请先从上方搜索并加入队列。");
            return;
        }

        int? removeIndex = null;
        int? moveUpIndex = null;
        int? moveDownIndex = null;

        var tableFlags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.Resizable
            | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##SelectedTable", 6, tableFlags))
        {
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch, 0.34f);
            ImGui.TableSetupColumn("工票", ImGuiTableColumnFlags.WidthStretch, 0.18f);
            ImGui.TableSetupColumn("成本", ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableSetupColumn("目标数量", ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableSetupColumn("排序", ImGuiTableColumnFlags.WidthFixed, 78f);
            ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableHeadersRow();

            for (var index = 0; index < _plugin.Config.ScripShopItems.Count; index++)
            {
                var item = _plugin.Config.ScripShopItems[index];
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
                var previousQuantity = quantity;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt("##Quantity", ref quantity, 0, 0))
                    item.Quantity = Math.Max(1, quantity);

                if (ImGui.IsItemDeactivatedAfterEdit() && item.Quantity != previousQuantity)
                    _plugin.Config.Save();

                ImGui.TableSetColumnIndex(4);
                ImGui.BeginDisabled(index == 0);
                if (ImGui.SmallButton("↑"))
                    moveUpIndex = index;
                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(index == _plugin.Config.ScripShopItems.Count - 1);
                if (ImGui.SmallButton("↓"))
                    moveDownIndex = index;
                ImGui.EndDisabled();

                ImGui.TableSetColumnIndex(5);
                if (ImGui.SmallButton("删除"))
                    removeIndex = index;

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        if (removeIndex.HasValue)
        {
            _plugin.Config.ScripShopItems.RemoveAt(removeIndex.Value);
            _plugin.Config.Save();
        }

        if (moveUpIndex.HasValue)
        {
            SwapItems(moveUpIndex.Value, moveUpIndex.Value - 1);
            _plugin.Config.Save();
        }

        if (moveDownIndex.HasValue)
        {
            SwapItems(moveDownIndex.Value, moveDownIndex.Value + 1);
            _plugin.Config.Save();
        }
    }

    private void SwapItems(int firstIndex, int secondIndex)
    {
        var list = _plugin.Config.ScripShopItems;
        (list[firstIndex], list[secondIndex]) = (list[secondIndex], list[firstIndex]);
    }
}
