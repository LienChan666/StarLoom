using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace Starloom.UI;

public sealed class StatusOverlay : Window
{
    private readonly Plugin _plugin;

    public StatusOverlay(Plugin plugin)
        : base("Starloom 状态##StarloomStatusOverlay")
    {
        _plugin = plugin;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new Vector2(320, 0), ImGuiCond.FirstUseEver);
    }

    public override void Draw()
    {
        ImGui.Text($"状态：{_plugin.GetOrchestratorStateText()}");
        ImGui.Text($"任务：{_plugin.GetCurrentJobDisplayName()}");
        ImGui.TextWrapped($"进度：{_plugin.GetCurrentStatusText()}");

        ImGui.Separator();

        ImGui.BeginDisabled(!_plugin.IsAutomationBusy);
        if (ImGui.Button("停止", new Vector2(120, 0)))
            _plugin.StopAutomation();
        ImGui.EndDisabled();
    }
}
