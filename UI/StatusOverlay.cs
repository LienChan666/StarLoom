using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StarLoom.UI.Components.Shared;
using System.Numerics;

namespace StarLoom.UI;

public sealed class StatusOverlay : Window
{
    private readonly Plugin _plugin;

    public StatusOverlay(Plugin plugin)
        : base("Starloom##StarloomStatusOverlay")
    {
        _plugin = plugin;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new Vector2(320, 0), ImGuiCond.FirstUseEver);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, GamePanelStyle.Layer1);
        ImGui.PushStyleColor(ImGuiCol.Border, GamePanelStyle.BorderAccent);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 10f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }

    public override void Draw()
    {
        var statusColor = _plugin.IsAutomationBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawStatusDot(statusColor);
        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextPrimary);
        ImGui.Text(_plugin.GetText("overlay.total_state", _plugin.GetOrchestratorStateText()));
        ImGui.PopStyleColor();

        GamePanelStyle.DrawGradientSeparator();

        ImGui.BeginDisabled(!_plugin.IsAutomationBusy);
        if (GamePanelStyle.DrawActionButton("overlay.stop", _plugin.GetText("common.stop"), GamePanelStyle.Danger, 120f, _plugin.IsAutomationBusy, "■"))
            _plugin.StopAutomation();
        ImGui.EndDisabled();
    }
}
