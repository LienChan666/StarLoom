using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System.Numerics;

namespace StarLoom.UI;

public sealed class StatusOverlay : Window
{
    private readonly IPluginUiFacade _ui;

    public StatusOverlay(IPluginUiFacade ui)
        : base("Starloom##StarloomStatusOverlay")
    {
        _ui = ui;
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
        var statusColor = _ui.IsAutomationBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawStatusDot(statusColor);
        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextPrimary);
        ImGui.Text(_ui.GetText("overlay.total_state", _ui.GetOrchestratorStateText()));
        ImGui.PopStyleColor();

        GamePanelStyle.DrawGradientSeparator();

        ImGui.BeginDisabled(!_ui.IsAutomationBusy);
        if (GamePanelStyle.DrawActionButton("overlay.stop", _ui.GetText("common.stop"), GamePanelStyle.Danger, 120f, _ui.IsAutomationBusy, "■"))
            _ui.StopAutomation();
        ImGui.EndDisabled();
    }
}

