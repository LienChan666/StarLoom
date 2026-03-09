using Dalamud.Bindings.ImGui;
using Starloom.Automation;
using Starloom.UI.Components.Shared;
using System;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class HomeControlPane
{
    public void Draw(Vector2 size)
    {
        using var _ = GamePanelStyle.BeginPanel("##HomeControlPane", size, GamePanelStyle.BorderAccent, GamePanelStyle.Gold);
        GamePanelStyle.DrawPanelHeader(P.Localization.Get("home.control.title"), P.Localization.Get("home.control.description"));

        DrawStatusSection();
        ImGui.Spacing();
        DrawArtisanListSection();
        ImGui.Spacing();
        DrawPrimaryActions();
        ImGui.Spacing();
        DrawQuickActions();
        ImGui.Spacing();
        GamePanelStyle.DrawHint(P.Localization.Get("home.control.hint"));
    }

    private static void DrawStatusSection()
    {
        GamePanelStyle.DrawSectionTitle(P.Localization.Get("home.control.total_state.title"), P.Localization.Get("home.control.total_state.description"));
        GamePanelStyle.DrawGradientSeparator();

        var schedulerColor = P.Automation.IsBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawStatusDot(schedulerColor);
        GamePanelStyle.DrawInfoRow("home.control.total_state", P.Localization.Get("common.state"), GetOrchestratorStateText());
        GamePanelStyle.DrawInfoRow("home.control.current_list", P.Localization.Get("common.current_list"), C.ArtisanListId.ToString());
    }

    private static void DrawArtisanListSection()
    {
        GamePanelStyle.DrawSectionTitle(P.Localization.Get("home.control.artisan_list.title"), P.Localization.Get("home.control.artisan_list.description"));

        var artisanListId = C.ArtisanListId;
        var previousArtisanListId = artisanListId;

        GamePanelStyle.DrawSettingLabel(P.Localization.Get("home.control.artisan_list.input_label"));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputInt("##HomeArtisanListId", ref artisanListId, 0, 0))
            C.ArtisanListId = Math.Max(0, artisanListId);

        if (ImGui.IsItemDeactivatedAfterEdit() && C.ArtisanListId != previousArtisanListId)
            P.ConfigStore.Save();

        GamePanelStyle.DrawHint(P.Localization.Get("home.control.artisan_list.hint"));
    }

    private static void DrawPrimaryActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = P.Automation.IsBusy;

        GamePanelStyle.DrawSectionTitle(P.Localization.Get("home.control.workflow.title"), P.Localization.Get("home.control.workflow.description"));
        if (GamePanelStyle.DrawActionButton("home.control.start", P.Localization.Get("common.start"), GamePanelStyle.Accent, buttonWidth, !isRunning, "▶"))
            P.Automation.StartConfiguredWorkflow();
        if (GamePanelStyle.DrawActionButton("home.control.stop", P.Localization.Get("common.stop"), GamePanelStyle.Danger, buttonWidth, isRunning, "■"))
            P.Automation.Stop();
    }

    private static void DrawQuickActions()
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;
        var isRunning = P.Automation.IsBusy;
        var hasConfiguredPurchases = P.Automation.HasConfiguredPurchases;

        GamePanelStyle.DrawSectionTitle(P.Localization.Get("home.control.quick.title"), P.Localization.Get("home.control.quick.description"));
        if (GamePanelStyle.DrawActionButton("home.control.turn_in", P.Localization.Get("home.control.quick.turn_in"), GamePanelStyle.Success, buttonWidth, !isRunning, "↑"))
            P.Automation.StartCollectableTurnIn();
        if (GamePanelStyle.DrawActionButton("home.control.purchase", P.Localization.Get("home.control.quick.purchase"), GamePanelStyle.Warning, buttonWidth, !isRunning && hasConfiguredPurchases, "↓"))
            P.Automation.StartPurchaseOnly();

        if (!hasConfiguredPurchases)
            GamePanelStyle.DrawHint(P.Localization.Get("home.control.quick.hint_purchase_required"));
    }

    private static string GetOrchestratorStateText()
    {
        if (P.Session.State != ArtisanSessionState.Idle)
            return P.Localization.Get(P.Session.GetStateKey());

        var key = P.TM.IsBusy ? "state.orchestrator.running" : "state.orchestrator.idle";
        return P.Localization.Get(key);
    }
}
