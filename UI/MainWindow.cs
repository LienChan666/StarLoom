using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Starloom.UI.Components.Home;
using Starloom.UI.Components.Settings;
using Starloom.UI.Components.Shared;
using System;
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

        DrawTab("home", P.Localization.Get("main.tab.home"), () => homeTab.Draw());
        DrawTab("settings", P.Localization.Get("main.tab.settings"), () => settingsTab.Draw());

        ImGui.EndTabBar();
    }

    private static void DrawTab(string id, string label, Action drawContent)
    {
        if (ImGui.BeginTabItem($"{label}##{id}"))
        {
            var drawList = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            drawList.AddLine(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), ImGui.GetColorU32(GamePanelStyle.Accent), 2f);
            drawContent();
            ImGui.EndTabItem();
        }
    }
}
