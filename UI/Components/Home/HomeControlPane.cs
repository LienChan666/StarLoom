using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class HomeControlPane
{
    public void Draw()
    {
        DrawStatusSection();
        ImGui.Spacing();
        DrawArtisanListSection();
        ImGui.Separator();
        ImGui.Spacing();
        DrawPrimaryActions();
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
            artisanListId = Math.Max(0, artisanListId);

        if (ImGui.IsItemDeactivatedAfterEdit() && artisanListId != previousArtisanListId)
            P.ConfigEditor.SetArtisanListId(artisanListId);
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
        return P.Localization.Get(P.Automation.GetStateKey());
    }
}
