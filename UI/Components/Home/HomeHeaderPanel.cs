using Dalamud.Bindings.ImGui;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System.Numerics;

namespace StarLoom.UI.Components.Home;

internal sealed class HomeHeaderPanel
{
    private readonly IPluginUiFacade _ui;

    public HomeHeaderPanel(IPluginUiFacade ui)
    {
        _ui = ui;
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
        ImGui.TextUnformatted(_ui.GetText("home.header.title"));
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1.0f);

        ImGui.TextUnformatted(_ui.GetText("home.header.subtitle"));
        GamePanelStyle.DrawHint(_ui.GetText("home.header.hint"));
    }

    private void DrawBadges()
    {
        var stateColor = _ui.IsAutomationBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawPillBadge(_ui.GetText("home.header.badge.total_state", _ui.GetOrchestratorStateText()), stateColor);
        ImGui.SameLine(0f, GamePanelStyle.Spacing.Sm);
        GamePanelStyle.DrawPillBadge(_ui.GetText("home.header.badge.current_list", _ui.Config.ArtisanListId), GamePanelStyle.AccentSoft);
    }
}

