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
    }

    private readonly Plugin _plugin;
    private readonly ShopSettingsCard _shopSettingsCard;
    private readonly CraftPointSettingsCard _craftPointSettingsCard;
    private readonly PurchaseSettingsCard _purchaseSettingsCard;
    private readonly DisplaySettingsCard _displaySettingsCard;
    private SettingsSection _selectedSection = SettingsSection.Shop;

    public SettingsTab(Plugin plugin, PluginUi pluginUi)
    {
        _plugin = plugin;
        _shopSettingsCard = new ShopSettingsCard(plugin);
        _craftPointSettingsCard = new CraftPointSettingsCard(plugin);
        _purchaseSettingsCard = new PurchaseSettingsCard(plugin);
        _displaySettingsCard = new DisplaySettingsCard(plugin, pluginUi);
    }

    public void Draw()
    {
        var availableSize = ImGui.GetContentRegionAvail();
        var spacing = GamePanelStyle.Spacing.Md;
        var navigationWidth = Math.Clamp(availableSize.X * 0.24f, 210f, 250f);
        var contentWidth = Math.Max(0f, availableSize.X - navigationWidth - spacing);

        using (GamePanelStyle.BeginPanel("##SettingsSidebar", new Vector2(navigationWidth, availableSize.Y), GamePanelStyle.BorderSubtle, GamePanelStyle.Accent))
        {
            DrawSidebar();
        }

        ImGui.SameLine(0f, spacing);

        using (GamePanelStyle.BeginPanel("##SettingsContent", new Vector2(contentWidth, availableSize.Y), GamePanelStyle.BorderSubtle))
        {
            DrawSelectedSection();
            ImGui.Spacing();
            GamePanelStyle.DrawHint(_plugin.GetText("settings.footer.hint"));
        }
    }

    private void DrawSidebar()
    {
        GamePanelStyle.DrawPanelHeader(_plugin.GetText("settings.sidebar.title"), _plugin.GetText("settings.sidebar.description"));

        DrawSidebarItem(SettingsSection.Shop, _plugin.GetText("settings.sidebar.shop"));
        DrawSidebarItem(SettingsSection.CraftPoint, _plugin.GetText("settings.sidebar.craft_point"));
        DrawSidebarItem(SettingsSection.Purchase, _plugin.GetText("settings.sidebar.purchase"));
        DrawSidebarItem(SettingsSection.Display, _plugin.GetText("settings.sidebar.display"));
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
                    _plugin.GetText("settings.card.shop.title"),
                    _plugin.GetText("settings.card.shop.description"),
                    _shopSettingsCard.Draw);
                break;
            case SettingsSection.CraftPoint:
                SettingsCard.Draw(
                    "##SettingsCraftPointCard",
                    _plugin.GetText("settings.card.craft_point.title"),
                    _plugin.GetText("settings.card.craft_point.description"),
                    _craftPointSettingsCard.Draw);
                break;
            case SettingsSection.Purchase:
                SettingsCard.Draw(
                    "##SettingsPurchaseCard",
                    _plugin.GetText("settings.card.purchase.title"),
                    _plugin.GetText("settings.card.purchase.description"),
                    _purchaseSettingsCard.Draw);
                break;
            case SettingsSection.Display:
                SettingsCard.Draw(
                    "##SettingsDisplayCard",
                    _plugin.GetText("settings.card.display.title"),
                    _plugin.GetText("settings.card.display.description"),
                    _displaySettingsCard.Draw);
                break;
        }
    }
}
