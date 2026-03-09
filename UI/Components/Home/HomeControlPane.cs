using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Home;

internal sealed class HomeControlPane
{
    private readonly Plugin _plugin;

    public HomeControlPane(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##HomeControlPane", size, GamePanelStyle.BorderAccent, GamePanelStyle.Gold);
        GamePanelStyle.DrawPanelHeader(_plugin.GetText("home.control.title"), _plugin.GetText("home.control.description"));

        DrawStatusSection();
        ImGui.Spacing();
        DrawArtisanListSection();
        ImGui.Spacing();
        DrawPrimaryActions();
        ImGui.Spacing();
        DrawQuickActions();
        ImGui.Spacing();
        GamePanelStyle.DrawHint(_plugin.GetText("home.control.hint"));
    }

    private void DrawStatusSection()
    {
        GamePanelStyle.DrawSectionTitle(_plugin.GetText("home.control.total_state.title"), _plugin.GetText("home.control.total_state.description"));
        GamePanelStyle.DrawGradientSeparator();

        var schedulerColor = _plugin.IsAutomationBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawStatusDot(schedulerColor);
        GamePanelStyle.DrawInfoRow("home.control.total_state", _plugin.GetText("common.state"), _plugin.GetOrchestratorStateText());
        GamePanelStyle.DrawInfoRow("home.control.current_list", _plugin.GetText("common.current_list"), _plugin.Config.ArtisanListId.ToString());
    }

    private void DrawArtisanListSection()
    {
        GamePanelStyle.DrawSectionTitle(_plugin.GetText("home.control.artisan_list.title"), _plugin.GetText("home.control.artisan_list.description"));

        var artisanListId = _plugin.Config.ArtisanListId;
        var previousArtisanListId = artisanListId;

        GamePanelStyle.DrawSettingLabel(_plugin.GetText("home.control.artisan_list.input_label"));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputInt("##HomeArtisanListId", ref artisanListId, 0, 0))
            _plugin.Config.ArtisanListId = Math.Max(0, artisanListId);

        if (ImGui.IsItemDeactivatedAfterEdit() && _plugin.Config.ArtisanListId != previousArtisanListId)
            _plugin.SaveConfig();

        GamePanelStyle.DrawHint(_plugin.GetText("home.control.artisan_list.hint"));
    }

    private void DrawPrimaryActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = _plugin.IsAutomationBusy;

        GamePanelStyle.DrawSectionTitle(_plugin.GetText("home.control.workflow.title"), _plugin.GetText("home.control.workflow.description"));
        if (GamePanelStyle.DrawActionButton("home.control.start", _plugin.GetText("common.start"), GamePanelStyle.Accent, buttonWidth, !isRunning, "▶"))
            _plugin.StartConfiguredWorkflow();
        if (GamePanelStyle.DrawActionButton("home.control.stop", _plugin.GetText("common.stop"), GamePanelStyle.Danger, buttonWidth, isRunning, "■"))
            _plugin.StopAutomation();
    }

    private void DrawQuickActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = _plugin.IsAutomationBusy;
        var hasConfiguredPurchases = _plugin.HasConfiguredPurchases;

        GamePanelStyle.DrawSectionTitle(_plugin.GetText("home.control.quick.title"), _plugin.GetText("home.control.quick.description"));
        if (GamePanelStyle.DrawActionButton("home.control.turn_in", _plugin.GetText("home.control.quick.turn_in"), GamePanelStyle.Success, buttonWidth, !isRunning, "↑"))
            _plugin.StartCollectableTurnIn();
        if (GamePanelStyle.DrawActionButton("home.control.purchase", _plugin.GetText("home.control.quick.purchase"), GamePanelStyle.Warning, buttonWidth, !isRunning && hasConfiguredPurchases, "↓"))
            _plugin.StartPurchaseOnly();

        if (!hasConfiguredPurchases)
            GamePanelStyle.DrawHint(_plugin.GetText("home.control.quick.hint_purchase_required"));
    }
}
