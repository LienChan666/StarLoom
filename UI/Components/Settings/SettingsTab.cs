using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System.Numerics;

namespace Starloom.UI.Components.Settings;

internal sealed class SettingsTab
{
    private readonly ShopSettingsCard shopSettingsCard = new();
    private readonly CraftPointSettingsCard craftPointSettingsCard = new();
    private readonly PurchaseSettingsCard purchaseSettingsCard = new();
    private readonly DisplaySettingsCard displaySettingsCard = new();

    public void Draw()
    {
        var availableSize = ImGui.GetContentRegionAvail();
        var layout = LayoutMetrics.CreateSettings(availableSize.X, 0f);

        if (!ImGui.BeginChild("##SettingsContent", new Vector2(layout.ContentWidth, availableSize.Y), true))
        {
            ImGui.EndChild();
            return;
        }

        if (ImGui.BeginTabBar("##SettingsSections"))
        {
            DrawSection(P.Localization.Get("settings.sidebar.shop"), shopSettingsCard.Draw);
            DrawSection(P.Localization.Get("settings.sidebar.craft_point"), craftPointSettingsCard.Draw);
            DrawSection(P.Localization.Get("settings.sidebar.purchase"), purchaseSettingsCard.Draw);
            DrawSection(P.Localization.Get("settings.sidebar.display"), displaySettingsCard.Draw);
            ImGui.EndTabBar();
        }

        ImGui.EndChild();
    }

    private static void DrawSection(string title, System.Action drawContent)
    {
        if (!ImGui.BeginTabItem(title))
            return;

        drawContent();
        ImGui.EndTabItem();
    }
}
