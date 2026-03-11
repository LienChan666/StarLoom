using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Tasks;
using StarLoom.Ui;
using StarLoom.Ui.Pages;
using System.Numerics;

namespace StarLoom.Ui.Components.Home;

internal sealed class HomeControlPane
{
    private readonly WorkflowTask workflowTask;
    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public HomeControlPane(WorkflowTask workflowTask, ConfigStore configStore, UiText uiText)
    {
        this.workflowTask = workflowTask;
        this.configStore = configStore;
        this.uiText = uiText;
    }

    public void Draw(Vector2 size)
    {
        if (!ImGui.BeginChild("##HomeControlPane", size, true))
        {
            ImGui.EndChild();
            return;
        }

        var state = HomePageState.FromWorkflow(workflowTask.isBusy, configStore.pluginConfig.scripShopItems.Count > 0);

        ImGui.TextUnformatted(uiText.Get("home.control.title"));
        ImGui.Separator();
        DrawStatusSection();
        ImGui.Spacing();
        DrawArtisanListSection();
        ImGui.Separator();
        ImGui.Spacing();
        DrawPrimaryActions(state);
        ImGui.Spacing();
        DrawQuickActions(state);
        ImGui.EndChild();
    }

    private void DrawStatusSection()
    {
        if (!ImGui.BeginTable("##HomeStatusTable", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            return;

        ImGui.TableSetupColumn("##HomeStatusLabel", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("##HomeStatusValue", ImGuiTableColumnFlags.WidthStretch);

        DrawStatusRow(uiText.Get("common.state"), uiText.Get(workflowTask.GetStateKey()));
        DrawStatusRow(uiText.Get("common.current_list"), configStore.pluginConfig.artisanListId.ToString());

        ImGui.EndTable();
    }

    private void DrawArtisanListSection()
    {
        var artisanListId = configStore.pluginConfig.artisanListId;
        var previousArtisanListId = artisanListId;

        ImGui.TextUnformatted(uiText.Get("home.control.artisan_list.input_label"));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputInt("##HomeArtisanListId", ref artisanListId, 0, 0))
            configStore.pluginConfig.artisanListId = Math.Max(0, artisanListId);

        if (ImGui.IsItemDeactivatedAfterEdit() && configStore.pluginConfig.artisanListId != previousArtisanListId)
            configStore.Save();
    }

    private void DrawPrimaryActions(HomePageState state)
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;

        ImGui.BeginDisabled(!state.canStartConfiguredWorkflow);
        if (ImGui.Button(uiText.Get("common.start"), new Vector2(buttonWidth, 0f)))
            workflowTask.StartConfiguredWorkflow();
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!state.canStop);
        if (ImGui.Button(uiText.Get("common.stop"), new Vector2(buttonWidth, 0f)))
            workflowTask.Stop();
        ImGui.EndDisabled();
    }

    private void DrawQuickActions(HomePageState state)
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X;

        ImGui.BeginDisabled(!state.canStartTurnInOnly);
        if (ImGui.Button(uiText.Get("home.control.quick.turn_in"), new Vector2(buttonWidth, 0f)))
            workflowTask.StartTurnInOnly();
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!state.canStartPurchaseOnly);
        if (ImGui.Button(uiText.Get("home.control.quick.purchase"), new Vector2(buttonWidth, 0f)))
            workflowTask.StartPurchaseOnly();
        ImGui.EndDisabled();

        if (state.showPurchaseRequirementHint)
            ImGui.TextDisabled(uiText.Get("home.control.quick.hint_purchase_required"));
    }

    private static void DrawStatusRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(value);
    }
}
