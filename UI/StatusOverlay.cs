using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Starloom.Automation;
using Starloom.UI.Components.Shared;
using System.Numerics;

namespace Starloom.UI;

public sealed class StatusOverlay : Window
{
    public StatusOverlay() : base("Starloom##StarloomStatusOverlay")
    {
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
        var statusColor = P.Automation.IsBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawStatusDot(statusColor);
        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextPrimary);

        var stateText = GetOrchestratorStateText();
        ImGui.Text(P.Localization.Format("overlay.total_state", stateText));
        ImGui.PopStyleColor();

        GamePanelStyle.DrawGradientSeparator();

        ImGui.BeginDisabled(!P.Automation.IsBusy);
        if (GamePanelStyle.DrawActionButton("overlay.stop", P.Localization.Get("common.stop"), GamePanelStyle.Danger, 120f, P.Automation.IsBusy, "■"))
            P.Automation.Stop();
        ImGui.EndDisabled();
    }

    private static string GetOrchestratorStateText()
    {
        return P.Localization.Get(P.Automation.GetStateKey());
    }
}
