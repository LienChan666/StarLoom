using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StarLoom.UI.Components.Home;
using StarLoom.UI.Components.Settings;
using StarLoom.UI.Components.Shared;
using System;
using System.Numerics;

namespace StarLoom.UI;

public sealed class MainWindow : Window
{
    private readonly Plugin _plugin;
    private readonly HomeTab _homeTab;
    private readonly SettingsTab _settingsTab;

    public MainWindow(Plugin plugin, PluginUi pluginUi)
        : base("Starloom###StarloomMainWindow")
    {
        _plugin = plugin;
        _homeTab = new HomeTab(plugin);
        _settingsTab = new SettingsTab(plugin, pluginUi);
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new Vector2(1180, 760), ImGuiCond.FirstUseEver);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, GamePanelStyle.Layer0);
        ImGui.PushStyleColor(ImGuiCol.Border, GamePanelStyle.BorderSubtle);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(GamePanelStyle.Spacing.Lg, GamePanelStyle.Spacing.Lg));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##StarloomTabs"))
            return;

        DrawTab("home", _plugin.GetText("main.tab.home"), () => _homeTab.Draw());
        DrawTab("settings", _plugin.GetText("main.tab.settings"), () => _settingsTab.Draw());

        ImGui.EndTabBar();
    }

    private static void DrawTab(string id, string label, Action drawContent)
    {
        if (ImGui.BeginTabItem($"{label}##{id}"))
        {
            var drawList = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            drawList.AddLine(
                new Vector2(min.X, max.Y),
                new Vector2(max.X, max.Y),
                ImGui.GetColorU32(GamePanelStyle.Accent),
                2f);

            drawContent();
            ImGui.EndTabItem();
        }
    }
}
