using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System.Numerics;

namespace Starloom.UI.Components.Settings;

internal sealed class SettingsTab
{
    private const float PaneSpacing = 12f;

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
        var layout = LayoutMetrics.CreateSettings(availableSize.X, PaneSpacing);

        if (ImGui.BeginChild("##SettingsSidebar", new Vector2(layout.NavigationWidth, availableSize.Y), true))
        {
            DrawSidebar();
            ImGui.EndChild();
        }

        ImGui.SameLine(0f, PaneSpacing);

        if (ImGui.BeginChild("##SettingsContent", new Vector2(layout.ContentWidth, availableSize.Y), true))
        {
            DrawSelectedSection();
            ImGui.EndChild();
        }
    }

    private void DrawSidebar()
    {
        DrawSidebarItem(SettingsSection.Shop, P.Localization.Get("settings.sidebar.shop"));
        DrawSidebarItem(SettingsSection.CraftPoint, P.Localization.Get("settings.sidebar.craft_point"));
        DrawSidebarItem(SettingsSection.Purchase, P.Localization.Get("settings.sidebar.purchase"));
        DrawSidebarItem(SettingsSection.Display, P.Localization.Get("settings.sidebar.display"));
    }

    private void DrawSidebarItem(SettingsSection section, string title)
    {
        var isSelected = selectedSection == section;
        if (ImGui.Selectable($"{title}##{section}", isSelected, ImGuiSelectableFlags.None, new Vector2(-1f, 28f)))
            selectedSection = section;

        ImGui.Spacing();
    }

    private void DrawSelectedSection()
    {
        switch (selectedSection)
        {
            case SettingsSection.Shop:
                shopSettingsCard.Draw();
                break;
            case SettingsSection.CraftPoint:
                craftPointSettingsCard.Draw();
                break;
            case SettingsSection.Purchase:
                purchaseSettingsCard.Draw();
                break;
            case SettingsSection.Display:
                displaySettingsCard.Draw();
                break;
        }
    }
}
