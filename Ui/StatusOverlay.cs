using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StarLoom.Config;
using StarLoom.Tasks;
using StarLoom.Ui.Components.Shared;
using System.Numerics;

namespace StarLoom.Ui;

public sealed class StatusOverlay : Window
{
    private readonly WorkflowTask workflowTask;
    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public StatusOverlay(WorkflowTask workflowTask, ConfigStore configStore, UiText uiText)
        : base("StarLoom##StarLoomStatusOverlay")
    {
        this.workflowTask = workflowTask;
        this.configStore = configStore;
        this.uiText = uiText;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new Vector2(320f, 0f), ImGuiCond.FirstUseEver);
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
        var statusColor = workflowTask.isBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawStatusDot(statusColor);
        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.TextPrimary);
        ImGui.Text(uiText.Format("overlay.total_state", uiText.Get(workflowTask.GetStateKey())));
        ImGui.PopStyleColor();

        GamePanelStyle.DrawGradientSeparator();

        ImGui.BeginDisabled(!workflowTask.isBusy);
        if (GamePanelStyle.DrawActionButton("overlay.stop", uiText.Get("common.stop"), GamePanelStyle.Danger, 120f, workflowTask.isBusy, "■"))
            workflowTask.Stop();
        ImGui.EndDisabled();

        var showOverlay = configStore.pluginConfig.showStatusOverlay;
        if (showOverlay != IsOpen)
        {
            configStore.pluginConfig.showStatusOverlay = IsOpen;
            configStore.Save();
        }
    }
}
