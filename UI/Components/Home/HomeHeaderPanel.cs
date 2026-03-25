using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;
using System.Numerics;

namespace StarLoom.UI.Components.Home;

internal sealed class HomeHeaderPanel
{
    private readonly Plugin _plugin;

    public HomeHeaderPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##HomeHeaderPanel", size, GamePanelStyle.BorderAccent, GamePanelStyle.Accent);
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
        ImGui.SetWindowFontScale(1.3f);
        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.Accent);
        ImGui.TextUnformatted("Starloom");
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1.0f);

        ImGui.TextUnformatted("收藏品提交 / 工票兑换控制台");
        GamePanelStyle.DrawHint("把流程控制、运行状态与兑换队列聚合到一套更接近 FF14 插件的深色工具面板中。");
    }

    private void DrawBadges()
    {
        var stateColor = _plugin.IsAutomationBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawPillBadge($"调度器 · {_plugin.GetOrchestratorStateText()}", stateColor);
        ImGui.SameLine(0f, GamePanelStyle.Spacing.Sm);
        GamePanelStyle.DrawPillBadge($"当前任务 · {_plugin.GetCurrentJobDisplayName()}", GamePanelStyle.Accent);
        ImGui.SameLine(0f, GamePanelStyle.Spacing.Sm);
        GamePanelStyle.DrawPillBadge($"当前清单 · {_plugin.Config.ArtisanListId}", GamePanelStyle.AccentSoft);
    }
}
