using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Starloom.UI.Components.Home;
using Starloom.UI.Components.Settings;
using System.Numerics;

namespace Starloom.UI;

public sealed class MainWindow : Window
{
    private readonly HomeTab homeTab;
    private readonly SettingsTab settingsTab;

    public MainWindow() : base("Starloom###StarloomMainWindow")
    {
        homeTab = new HomeTab();
        settingsTab = new SettingsTab();
    }

    public override void PreDraw()
        => ImGui.SetNextWindowSize(new Vector2(1180, 760), ImGuiCond.FirstUseEver);

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##StarloomTabs"))
            return;

        if (ImGui.BeginTabItem(P.Localization.Get("main.tab.home")))
        {
            homeTab.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(P.Localization.Get("main.tab.settings")))
        {
            settingsTab.Draw();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
