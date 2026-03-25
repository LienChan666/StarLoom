using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System;
using System.Numerics;

namespace Starloom.UI.Components.Settings;

internal sealed class SettingsTab
{
    private enum SettingsSection
    {
        Shop,
        CraftPoint,
        Purchase,
        Display,
    }

    private readonly ShopSettingsCard shopSettingsCard = new();
    private readonly CraftPointSettingsCard craftPointSettingsCard = new();
    private readonly PurchaseSettingsCard purchaseSettingsCard = new();
    private readonly DisplaySettingsCard displaySettingsCard = new();
    private SettingsSection selectedSection = SettingsSection.Shop;

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
            GamePanelStyle.DrawHint(P.Localization.Get("settings.footer.hint"));
        }
    }

    private void DrawSidebar()
    {
        GamePanelStyle.DrawPanelHeader(P.Localization.Get("settings.sidebar.title"), P.Localization.Get("settings.sidebar.description"));

        DrawSidebarItem(SettingsSection.Shop, P.Localization.Get("settings.sidebar.shop"));
        DrawSidebarItem(SettingsSection.CraftPoint, P.Localization.Get("settings.sidebar.craft_point"));
        DrawSidebarItem(SettingsSection.Purchase, P.Localization.Get("settings.sidebar.purchase"));
        DrawSidebarItem(SettingsSection.Display, P.Localization.Get("settings.sidebar.display"));
    }

    private void DrawSidebarItem(SettingsSection section, string title)
    {
        var isSelected = selectedSection == section;

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
            selectedSection = section;

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
        switch (selectedSection)
        {
            case SettingsSection.Shop:
                SettingsCard.Draw(
                    "##SettingsShopCard",
                    P.Localization.Get("settings.card.shop.title"),
                    P.Localization.Get("settings.card.shop.description"),
                    shopSettingsCard.Draw);
                break;
            case SettingsSection.CraftPoint:
                SettingsCard.Draw(
                    "##SettingsCraftPointCard",
                    P.Localization.Get("settings.card.craft_point.title"),
                    P.Localization.Get("settings.card.craft_point.description"),
                    craftPointSettingsCard.Draw);
                break;
            case SettingsSection.Purchase:
                SettingsCard.Draw(
                    "##SettingsPurchaseCard",
                    P.Localization.Get("settings.card.purchase.title"),
                    P.Localization.Get("settings.card.purchase.description"),
                    purchaseSettingsCard.Draw);
                break;
            case SettingsSection.Display:
                SettingsCard.Draw(
                    "##SettingsDisplayCard",
                    P.Localization.Get("settings.card.display.title"),
                    P.Localization.Get("settings.card.display.description"),
                    displaySettingsCard.Draw);
                break;
        }
    }
}
