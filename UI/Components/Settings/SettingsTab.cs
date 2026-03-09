using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Settings;

internal sealed class SettingsTab
{
    private enum SettingsSection
    {
        Shop,
        CraftPoint,
        Purchase,
        Display,
        Catalog,
    }

    private readonly ShopSettingsCard _shopSettingsCard;
    private readonly CraftPointSettingsCard _craftPointSettingsCard;
    private readonly PurchaseSettingsCard _purchaseSettingsCard;
    private readonly DisplaySettingsCard _displaySettingsCard;
    private readonly CatalogSettingsCard _catalogSettingsCard;
    private SettingsSection _selectedSection = SettingsSection.Shop;

    public SettingsTab(Plugin plugin, PluginUi pluginUi)
    {
        _shopSettingsCard = new ShopSettingsCard(plugin);
        _craftPointSettingsCard = new CraftPointSettingsCard(plugin);
        _purchaseSettingsCard = new PurchaseSettingsCard(plugin);
        _displaySettingsCard = new DisplaySettingsCard(plugin, pluginUi);
        _catalogSettingsCard = new CatalogSettingsCard(plugin);
    }

    public void Draw()
    {
        var availableSize = ImGui.GetContentRegionAvail();
        var spacing = GamePanelStyle.Spacing.Md;
        var navigationWidth = Math.Clamp(availableSize.X * 0.24f, 210f, 250f);
        var contentWidth = Math.Max(0f, availableSize.X - navigationWidth - spacing);

        using (var navigationPanel = GamePanelStyle.BeginPanel("##SettingsSidebar", new Vector2(navigationWidth, availableSize.Y), GamePanelStyle.BorderSubtle, GamePanelStyle.Accent))
        {
            DrawSidebar();
        }

        ImGui.SameLine(0f, spacing);

        using (var contentPanel = GamePanelStyle.BeginPanel("##SettingsContent", new Vector2(contentWidth, availableSize.Y), GamePanelStyle.BorderSubtle))
        {
            DrawSelectedSection();
            ImGui.Spacing();
            GamePanelStyle.DrawHint("以上配置均为即时保存。购买逻辑会根据兑换列表中的目标数量与当前背包数量自动计算仍需兑换的数量。");
        }
    }

    private void DrawSidebar()
    {
        GamePanelStyle.DrawPanelHeader("系统设置", "选择需要调整的功能模块。");

        DrawSidebarItem(SettingsSection.Shop, "商店设置");
        DrawSidebarItem(SettingsSection.CraftPoint, "制作点设置");
        DrawSidebarItem(SettingsSection.Purchase, "购买设置");
        DrawSidebarItem(SettingsSection.Display, "界面设置");
        DrawSidebarItem(SettingsSection.Catalog, "物品索引");
    }

    private void DrawSidebarItem(SettingsSection section, string title)
    {
        var isSelected = _selectedSection == section;

        if (isSelected)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, GamePanelStyle.Layer2);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, GamePanelStyle.Layer2);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, GamePanelStyle.Layer2);
            ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextPrimary);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0, 0, 0, 0));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(GamePanelStyle.Layer2.X, GamePanelStyle.Layer2.Y, GamePanelStyle.Layer2.Z, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, GamePanelStyle.Layer2);
            ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextSecond);
        }

        if (ImGui.Selectable($"{title}##{section}", isSelected, ImGuiSelectableFlags.None, new Vector2(-1f, 28f)))
            _selectedSection = section;

        if (isSelected)
        {
            var drawList = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            drawList.AddRectFilled(
                new Vector2(min.X, min.Y),
                new Vector2(min.X + 3f, max.Y),
                ImGui.GetColorU32(GamePanelStyle.Accent),
                2f);
        }

        ImGui.PopStyleColor(4);
        ImGui.Dummy(new Vector2(0, GamePanelStyle.Spacing.Xs));
    }

    private void DrawSelectedSection()
    {
        switch (_selectedSection)
        {
            case SettingsSection.Shop:
                SettingsCard.Draw(
                    "##SettingsShopCard",
                    "商店设置",
                    "选择自动交互使用的收藏品商店。",
                    _shopSettingsCard.Draw);
                break;
            case SettingsSection.CraftPoint:
                SettingsCard.Draw(
                    "##SettingsCraftPointCard",
                    "制作点设置",
                    "配置 Starloom 接管结束后的默认返回点；流程会自动回到住宅区、进屋，然后再恢复 Artisan。",
                    _craftPointSettingsCard.Draw);
                break;
            case SettingsSection.Purchase:
                SettingsCard.Draw(
                    "##SettingsPurchaseCard",
                    "购买设置",
                    "这些选项会影响自动兑换时机、工票预留和背包保护逻辑。",
                    _purchaseSettingsCard.Draw);
                break;
            case SettingsSection.Display:
                SettingsCard.Draw(
                    "##SettingsDisplayCard",
                    "界面设置",
                    "控制插件状态信息的显示方式。",
                    _displaySettingsCard.Draw);
                break;
            case SettingsSection.Catalog:
                SettingsCard.Draw(
                    "##SettingsCatalogCard",
                    "物品索引",
                    "兑换物品列表作为外部缓存运行时加载，支持手动重新构建。",
                    _catalogSettingsCard.Draw);
                break;
        }
    }
}
