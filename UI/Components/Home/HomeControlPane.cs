using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class HomeControlPane
{
    private readonly Plugin _plugin;

    public HomeControlPane(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##HomeControlPane", size, GamePanelStyle.Accent);
        GamePanelStyle.DrawPanelHeader("指挥面板", "监控自动流程、调整当前清单，并快速执行核心操作。");

        DrawStatusSection();
        ImGui.Spacing();
        DrawArtisanListSection();
        ImGui.Spacing();
        DrawPrimaryActions();
        ImGui.Spacing();
        DrawQuickActions();
        ImGui.Spacing();
        GamePanelStyle.DrawHint("“开始”会启动受管的 Artisan 清单；当背包空格低于阈值且存在可提交收藏品时，Starloom 会自动接管并执行提交 / 购买流程。若启动时空格已低于阈值，则会先做预检查，再决定是否先跑一轮 Starloom 流程。");
    }

    private void DrawStatusSection()
    {
        GamePanelStyle.DrawSectionTitle("运行状态", "当前调度器、任务与流程提示。");
        ImGui.Separator();
        GamePanelStyle.DrawInfoRow("调度器", _plugin.GetOrchestratorStateText());
        GamePanelStyle.DrawInfoRow("当前任务", _plugin.GetCurrentJobDisplayName());
        GamePanelStyle.DrawInfoRow("状态说明", _plugin.GetCurrentStatusText());
        GamePanelStyle.DrawInfoRow("当前清单", _plugin.Config.ArtisanListId.ToString());
    }

    private void DrawArtisanListSection()
    {
        GamePanelStyle.DrawSectionTitle("Artisan 清单", "Starloom 会从该清单入口开始接管流程。");

        var artisanListId = _plugin.Config.ArtisanListId;
        var previousArtisanListId = artisanListId;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputInt("Artisan 清单 ID##HomeArtisanListId", ref artisanListId, 0, 0))
            _plugin.Config.ArtisanListId = Math.Max(0, artisanListId);

        if (ImGui.IsItemDeactivatedAfterEdit() && _plugin.Config.ArtisanListId != previousArtisanListId)
            _plugin.Config.Save();

        GamePanelStyle.DrawHint("清单 ID 修改后会立即保存。若流程异常，优先检查这里是否指向你实际使用的 Artisan 清单。");
    }

    private void DrawPrimaryActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = _plugin.IsAutomationBusy;

        GamePanelStyle.DrawSectionTitle("主流程", "执行或中断完整的提交流程。");
        DrawActionButton("开始", GamePanelStyle.Accent, buttonWidth, !isRunning, _plugin.StartConfiguredWorkflow);
        DrawActionButton("停止", GamePanelStyle.Danger, buttonWidth, isRunning, _plugin.StopAutomation);
    }

    private void DrawQuickActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = _plugin.IsAutomationBusy;
        var hasConfiguredPurchases = _plugin.HasConfiguredPurchases;

        GamePanelStyle.DrawSectionTitle("快捷操作", "单独执行某一段流程，用于临时手动接管。");
        DrawActionButton("提交收藏品", GamePanelStyle.Success, buttonWidth, !isRunning, _plugin.StartCollectableTurnIn);
        DrawActionButton("购买物品", GamePanelStyle.Warning, buttonWidth, !isRunning && hasConfiguredPurchases, _plugin.StartPurchaseOnly);

        if (!hasConfiguredPurchases)
            GamePanelStyle.DrawHint("购买物品按钮需要右侧兑换队列中至少配置一项物品。");
    }

    private static void DrawActionButton(string label, Vector4 color, float width, bool enabled, Action action)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, GamePanelStyle.Tint(color, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, GamePanelStyle.Tint(color, 0.72f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, GamePanelStyle.Tint(color, 0.86f));
        ImGui.BeginDisabled(!enabled);
        if (ImGui.Button(label, new Vector2(width, 32f)))
            action();
        ImGui.EndDisabled();
        ImGui.PopStyleColor(3);
    }
}
