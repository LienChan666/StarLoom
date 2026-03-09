using Dalamud.Bindings.ImGui;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System;

namespace StarLoom.UI.Components.Settings;

internal sealed class DisplaySettingsCard
{
    private readonly IPluginUiFacade _ui;

    public DisplaySettingsCard(IPluginUiFacade ui)
    {
        _ui = ui;
    }

    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##DisplaySettingsTable"))
            return;

        var showStatusOverlay = _ui.Config.ShowStatusOverlay;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.display.overlay"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox($"{_ui.GetText("settings.display.overlay_toggle")}##DisplayOverlay", ref showStatusOverlay))
        {
            _ui.SetStatusOverlayVisible(showStatusOverlay);
            _ui.SaveConfig();
        }

        var uiLanguage = _ui.Config.UiLanguage;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.display.language"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##UiLanguage", _ui.GetText($"settings.display.language.{uiLanguage}")))
        {
            DrawLanguageOption("zh");
            DrawLanguageOption("en");
            ImGui.EndCombo();
        }

        ImGui.EndTable();
    }

    private void DrawLanguageOption(string language)
    {
        var isSelected = string.Equals(_ui.Config.UiLanguage, language, StringComparison.Ordinal);
        if (ImGui.Selectable($"{_ui.GetText($"settings.display.language.{language}")}##UiLanguage_{language}", isSelected))
        {
            _ui.Config.UiLanguage = language;
            _ui.ReloadLocalization();
            _ui.SaveConfig();
        }

        if (isSelected)
            ImGui.SetItemDefaultFocus();
    }
}

