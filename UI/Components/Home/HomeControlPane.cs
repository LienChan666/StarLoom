using Dalamud.Bindings.ImGui;
using Starloom.Automation;
using System;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class HomeControlPane
{
    public void Draw(Vector2 size)
    {
        if (!ImGui.BeginChild("##HomeControlPane", size, true))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.TextUnformatted(P.Localization.Get("home.control.title"));
        ImGui.Separator();
        DrawStatusSection();
        ImGui.Spacing();
        DrawArtisanListSection();
        ImGui.Separator();
        ImGui.Spacing();
        DrawPrimaryActions();
        ImGui.Spacing();
        DrawQuickActions();
        ImGui.EndChild();
    }

    private static void DrawStatusSection()
    {
        if (!ImGui.BeginTable("##HomeStatusTable", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            return;

        ImGui.TableSetupColumn("##HomeStatusLabel", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("##HomeStatusValue", ImGuiTableColumnFlags.WidthStretch);

        DrawStatusRow(P.Localization.Get("common.state"), GetOrchestratorStateText());
        DrawStatusRow(P.Localization.Get("common.current_list"), C.ArtisanListId.ToString());

        ImGui.EndTable();
    }

    private static void DrawArtisanListSection()
    {
        var artisanListId = C.ArtisanListId;
        var previousArtisanListId = artisanListId;

        ImGui.TextUnformatted(P.Localization.Get("home.control.artisan_list.input_label"));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputInt("##HomeArtisanListId", ref artisanListId, 0, 0))
            C.ArtisanListId = Math.Max(0, artisanListId);

        if (ImGui.IsItemDeactivatedAfterEdit() && C.ArtisanListId != previousArtisanListId)
            P.ConfigStore.Save();
    }

    private static void DrawPrimaryActions()
    {
        var isRunning = P.Automation.IsBusy;
        var buttonWidth = ImGui.GetContentRegionAvail().X;

        ImGui.BeginDisabled(isRunning);
        if (ImGui.Button(P.Localization.Get("common.start"), new Vector2(buttonWidth, 0f)))
            P.Automation.StartConfiguredWorkflow();
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!isRunning);
        if (ImGui.Button(P.Localization.Get("common.stop"), new Vector2(buttonWidth, 0f)))
            P.Automation.Stop();
        ImGui.EndDisabled();
    }

    private static void DrawQuickActions()
    {
        var isRunning = P.Automation.IsBusy;
        var hasConfiguredPurchases = P.Automation.HasConfiguredPurchases;
        var buttonWidth = ImGui.GetContentRegionAvail().X;

        ImGui.BeginDisabled(isRunning);
        if (ImGui.Button(P.Localization.Get("home.control.quick.turn_in"), new Vector2(buttonWidth, 0f)))
            P.Automation.StartCollectableTurnIn();
        ImGui.EndDisabled();

        ImGui.BeginDisabled(isRunning || !hasConfiguredPurchases);
        if (ImGui.Button(P.Localization.Get("home.control.quick.purchase"), new Vector2(buttonWidth, 0f)))
            P.Automation.StartPurchaseOnly();
        ImGui.EndDisabled();

        if (!hasConfiguredPurchases)
            ImGui.TextDisabled(P.Localization.Get("home.control.quick.hint_purchase_required"));
    }

    private static void DrawStatusRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(value);
    }

    private static string GetOrchestratorStateText()
    {
        if (P.Session.State != ArtisanSessionState.Idle)
            return P.Localization.Get(P.Session.GetStateKey());

        var key = P.TM.IsBusy ? "state.orchestrator.running" : "state.orchestrator.idle";
        return P.Localization.Get(key);
    }
}
