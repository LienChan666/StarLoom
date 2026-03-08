using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class HomeHeaderPanel
{
    private readonly Plugin _plugin;

    public HomeHeaderPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##HomeHeaderPanel", size, GamePanelStyle.Accent);
        if (!ImGui.BeginTable("##HomeHeaderLayout", 2, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("##HomeHeaderMain", ImGuiTableColumnFlags.WidthStretch, 0.58f);
        ImGui.TableSetupColumn("##HomeHeaderState", ImGuiTableColumnFlags.WidthStretch, 0.42f);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawSummary();

        ImGui.TableSetColumnIndex(1);
        DrawBadges();
        ImGui.EndTable();
    }

    private void DrawSummary()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.Accent);
        ImGui.TextUnformatted("Starloom");
        ImGui.PopStyleColor();

        ImGui.TextUnformatted("收藏品提交 / 工票兑换控制台");
        GamePanelStyle.DrawHint("把流程控制、运行状态与兑换队列聚合到一套更接近 FF14 插件的深色工具面板中。");
    }

    private void DrawBadges()
    {
        var stateColor = _plugin.IsAutomationBusy ? GamePanelStyle.Warning : GamePanelStyle.Success;
        GamePanelStyle.DrawInlineBadge("调度器", _plugin.GetOrchestratorStateText(), stateColor);
        GamePanelStyle.DrawInlineBadge("当前任务", _plugin.GetCurrentJobDisplayName(), GamePanelStyle.Accent);
        GamePanelStyle.DrawInlineBadge("当前清单", _plugin.Config.ArtisanListId.ToString(), GamePanelStyle.AccentSoft);
    }
}
