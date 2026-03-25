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
            P.ConfigEditor.SetUiLanguage(language);
            P.Localization.Reload();
        }

        if (isSelected)
            ImGui.SetItemDefaultFocus();
    }
}
