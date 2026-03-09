using Dalamud.Bindings.ImGui;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Home;

internal sealed class HomeControlPane
{
    private readonly IPluginUiFacade _ui;

    public HomeControlPane(IPluginUiFacade ui)
    {
        _ui = ui;
    }

    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##HomeControlPane", size, GamePanelStyle.BorderAccent, GamePanelStyle.Gold);
        GamePanelStyle.DrawPanelHeader(_ui.GetText("home.control.title"), _ui.GetText("home.control.description"));

        DrawStatusSection();
        ImGui.Spacing();
        DrawArtisanListSection();
        ImGui.Spacing();
        DrawPrimaryActions();
        ImGui.Spacing();
        DrawQuickActions();
        ImGui.Spacing();
        GamePanelStyle.DrawHint(_ui.GetText("home.control.hint"));
    }

    private void DrawStatusSection()
    {
        GamePanelStyle.DrawSectionTitle(_ui.GetText("home.control.total_state.title"), _ui.GetText("home.control.total_state.description"));
        GamePanelStyle.DrawGradientSeparator();

        var schedulerColor = _ui.IsAutomationBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawStatusDot(schedulerColor);
        GamePanelStyle.DrawInfoRow("home.control.total_state", _ui.GetText("common.state"), _ui.GetOrchestratorStateText());
        GamePanelStyle.DrawInfoRow("home.control.current_list", _ui.GetText("common.current_list"), _ui.Config.ArtisanListId.ToString());
    }

    private void DrawArtisanListSection()
    {
        GamePanelStyle.DrawSectionTitle(_ui.GetText("home.control.artisan_list.title"), _ui.GetText("home.control.artisan_list.description"));

        var artisanListId = _ui.Config.ArtisanListId;
        var previousArtisanListId = artisanListId;

        GamePanelStyle.DrawSettingLabel(_ui.GetText("home.control.artisan_list.input_label"));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputInt("##HomeArtisanListId", ref artisanListId, 0, 0))
            _ui.Config.ArtisanListId = Math.Max(0, artisanListId);

        if (ImGui.IsItemDeactivatedAfterEdit() && _ui.Config.ArtisanListId != previousArtisanListId)
            _ui.SaveConfig();

        GamePanelStyle.DrawHint(_ui.GetText("home.control.artisan_list.hint"));
    }

    private void DrawPrimaryActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = _ui.IsAutomationBusy;

        GamePanelStyle.DrawSectionTitle(_ui.GetText("home.control.workflow.title"), _ui.GetText("home.control.workflow.description"));
        if (GamePanelStyle.DrawActionButton("home.control.start", _ui.GetText("common.start"), GamePanelStyle.Accent, buttonWidth, !isRunning, "▶"))
            _ui.StartConfiguredWorkflow();
        if (GamePanelStyle.DrawActionButton("home.control.stop", _ui.GetText("common.stop"), GamePanelStyle.Danger, buttonWidth, isRunning, "■"))
            _ui.StopAutomation();
    }

    private void DrawQuickActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = _ui.IsAutomationBusy;
        var hasConfiguredPurchases = _ui.HasConfiguredPurchases;

        GamePanelStyle.DrawSectionTitle(_ui.GetText("home.control.quick.title"), _ui.GetText("home.control.quick.description"));
        if (GamePanelStyle.DrawActionButton("home.control.turn_in", _ui.GetText("home.control.quick.turn_in"), GamePanelStyle.Success, buttonWidth, !isRunning, "↑"))
            _ui.StartCollectableTurnIn();
        if (GamePanelStyle.DrawActionButton("home.control.purchase", _ui.GetText("home.control.quick.purchase"), GamePanelStyle.Warning, buttonWidth, !isRunning && hasConfiguredPurchases, "↓"))
            _ui.StartPurchaseOnly();

        if (!hasConfiguredPurchases)
            GamePanelStyle.DrawHint(_ui.GetText("home.control.quick.hint_purchase_required"));
    }
}

