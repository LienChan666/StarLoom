using Dalamud.Bindings.ImGui;
using Starloom.Automation;
using Starloom.UI.Components.Shared;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class HomeHeaderPanel
{
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

    private static void DrawSummary()
    {
        ImGui.SetWindowFontScale(1.3f);
        ImGui.PushStyleColor(ImGuiCol.Text, GamePanelStyle.Accent);
        ImGui.TextUnformatted(P.Localization.Get("home.header.title"));
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1.0f);

        ImGui.TextUnformatted(P.Localization.Get("home.header.subtitle"));
        GamePanelStyle.DrawHint(P.Localization.Get("home.header.hint"));
    }

    private static void DrawBadges()
    {
        var stateColor = P.Automation.IsBusy ? GamePanelStyle.Gold : GamePanelStyle.Success;
        GamePanelStyle.DrawPillBadge(P.Localization.Format("home.header.badge.total_state", GetOrchestratorStateText()), stateColor);
        ImGui.SameLine(0f, GamePanelStyle.Spacing.Sm);
        GamePanelStyle.DrawPillBadge(P.Localization.Format("home.header.badge.current_list", C.ArtisanListId), GamePanelStyle.AccentSoft);
    }

    private static string GetOrchestratorStateText()
    {
        return P.Localization.Get(P.Automation.GetStateKey());
    }
}
