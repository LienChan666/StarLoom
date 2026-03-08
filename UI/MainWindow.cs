using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Starloom.UI.Components.Home;
using Starloom.UI.Components.Settings;
using System.Numerics;

namespace Starloom.UI;

public sealed class MainWindow : Window
{
    private readonly HomeTab _homeTab;
    private readonly SettingsTab _settingsTab;

    public MainWindow(Plugin plugin, PluginUi pluginUi)
        : base("Starloom###StarloomMainWindow")
    {
        _homeTab = new HomeTab(plugin);
        _settingsTab = new SettingsTab(plugin, pluginUi);
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new Vector2(1180, 760), ImGuiCond.FirstUseEver);
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##StarloomTabs"))
            return;

        if (ImGui.BeginTabItem("主页"))
        {
            _homeTab.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("设置"))
        {
            _settingsTab.Draw();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
