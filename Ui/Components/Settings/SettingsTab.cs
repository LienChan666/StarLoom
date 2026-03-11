using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;
using System.Numerics;

namespace StarLoom.Ui.Components.Settings;

internal sealed class SettingsTab
{
    private readonly ShopSettingsCard shopSettingsCard;
    private readonly CraftPointSettingsCard craftPointSettingsCard;
    private readonly PurchaseSettingsCard purchaseSettingsCard;
    private readonly DisplaySettingsCard displaySettingsCard;
    private readonly UiText uiText;

    public SettingsTab(ConfigStore configStore, UiText uiText)
    {
        this.uiText = uiText;
        shopSettingsCard = new ShopSettingsCard(configStore, uiText);
        craftPointSettingsCard = new CraftPointSettingsCard(configStore, uiText);
        purchaseSettingsCard = new PurchaseSettingsCard(configStore, uiText);
        displaySettingsCard = new DisplaySettingsCard(configStore, uiText);
    }

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
            DrawSection(uiText.Get("settings.sidebar.shop"), shopSettingsCard.Draw);
            DrawSection(uiText.Get("settings.sidebar.craft_point"), craftPointSettingsCard.Draw);
            DrawSection(uiText.Get("settings.sidebar.purchase"), purchaseSettingsCard.Draw);
            DrawSection(uiText.Get("settings.sidebar.display"), displaySettingsCard.Draw);
            ImGui.EndTabBar();
        }

        ImGui.EndChild();
    }

    private static void DrawSection(string title, Action drawContent)
    {
        if (!ImGui.BeginTabItem(title))
            return;

        drawContent();
        ImGui.EndTabItem();
    }
}
