using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Home;

internal sealed class HomeTab
{
    private readonly HomeHeaderPanel _headerPanel;
    private readonly HomeControlPane _controlPane;
    private readonly SearchPane _searchPane;
    private readonly SelectedItemsPane _selectedItemsPane;

    public HomeTab(Plugin plugin)
    {
        _headerPanel = new HomeHeaderPanel(plugin);
        _controlPane = new HomeControlPane(plugin);
        _searchPane = new SearchPane(plugin);
        _selectedItemsPane = new SelectedItemsPane(plugin);
    }

    public void Draw()
    {
        var headerHeight = Math.Clamp(ImGui.GetContentRegionAvail().Y * 0.18f, 88f, 112f);
        _headerPanel.Draw(new Vector2(0f, headerHeight));

        ImGui.Dummy(new Vector2(0, GamePanelStyle.Spacing.Sm));
        DrawMainContent();
    }

    private void DrawMainContent()
    {
        var availableSize = ImGui.GetContentRegionAvail();
        var spacing = GamePanelStyle.Spacing.Md;
        var leftWidth = Math.Clamp(availableSize.X * 0.30f, 280f, 340f);
        var rightWidth = Math.Max(0f, availableSize.X - leftWidth - spacing);

        _controlPane.Draw(new Vector2(leftWidth, availableSize.Y));
        ImGui.SameLine(0f, spacing);

        using var _ = GamePanelStyle.BeginPanel("##HomeFlowShell", new Vector2(rightWidth, availableSize.Y), GamePanelStyle.BorderSubtle);
        GamePanelStyle.DrawPanelHeader("兑换编排区", "先搜索需要的工票物品，再在下方维护当前兑换队列。\n整个操作流保持在同一块面板内完成。");

        var contentHeight = ImGui.GetContentRegionAvail().Y;
        var verticalSpacing = GamePanelStyle.Spacing.Sm;
        var topHeight = Math.Max(190f, (contentHeight - verticalSpacing) * 0.44f);
        var remainingHeight = Math.Max(0f, contentHeight - topHeight - verticalSpacing);

        _searchPane.Draw(new Vector2(0f, topHeight));
        ImGui.Dummy(new Vector2(0, verticalSpacing));
        _selectedItemsPane.Draw(new Vector2(0f, remainingHeight));
    }
}
