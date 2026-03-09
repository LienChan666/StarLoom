using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Shared;
using System;

namespace Starloom.UI.Components.Settings;

internal sealed class DisplaySettingsCard
{
    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##DisplaySettingsTable"))
            return;

        var showStatusOverlay = C.ShowStatusOverlay;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(P.Localization.Get("settings.display.overlay"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox($"{P.Localization.Get("settings.display.overlay_toggle")}##DisplayOverlay", ref showStatusOverlay))
        {
            C.ShowStatusOverlay = showStatusOverlay;
            P.ConfigStore.Save();
        }

        var uiLanguage = C.UiLanguage;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(P.Localization.Get("settings.display.language"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##UiLanguage", P.Localization.Get($"settings.display.language.{uiLanguage}")))
        {
            DrawLanguageOption("zh");
            DrawLanguageOption("en");
            ImGui.EndCombo();
        }

        ImGui.EndTable();
    }

    private static void DrawLanguageOption(string language)
    {
        var isSelected = string.Equals(C.UiLanguage, language, StringComparison.Ordinal);
        if (ImGui.Selectable($"{P.Localization.Get($"settings.display.language.{language}")}##UiLanguage_{language}", isSelected))
        {
            C.UiLanguage = language;
            P.Localization.Reload();
            P.ConfigStore.Save();
        }

        if (isSelected)
            ImGui.SetItemDefaultFocus();
    }
}
