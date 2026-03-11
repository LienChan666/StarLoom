using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Ui.Components.Settings;
using StarLoom.Ui;

namespace StarLoom.Ui.Pages;

public sealed class SettingsPage
{
    private readonly SettingsTab settingsTab;

    public SettingsPage(ConfigStore configStore, UiText uiText)
    {
        settingsTab = new SettingsTab(configStore, uiText);
    }

    public void Draw()
    {
        settingsTab.Draw();
    }
}
