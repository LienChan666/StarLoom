using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;
using System;

namespace StarLoom.UI.Components.Settings;

internal sealed class DisplaySettingsCard
{
    private readonly Plugin _plugin;
    private readonly PluginUi _pluginUi;

    public DisplaySettingsCard(Plugin plugin, PluginUi pluginUi)
    {
        _plugin = plugin;
        _pluginUi = pluginUi;
    }

    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##DisplaySettingsTable"))
            return;

        var showStatusOverlay = _plugin.Config.ShowStatusOverlay;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_plugin.GetText("settings.display.overlay"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox($"{_plugin.GetText("settings.display.overlay_toggle")}##DisplayOverlay", ref showStatusOverlay))
            _pluginUi.SetStatusOverlayVisible(showStatusOverlay);

        var uiLanguage = _plugin.Config.UiLanguage;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_plugin.GetText("settings.display.language"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##UiLanguage", _plugin.GetText($"settings.display.language.{uiLanguage}")))
        {
            DrawLanguageOption("zh");
            DrawLanguageOption("en");
            ImGui.EndCombo();
        }

        ImGui.EndTable();
    }

    private void DrawLanguageOption(string language)
    {
        var isSelected = string.Equals(_plugin.Config.UiLanguage, language, StringComparison.Ordinal);
        if (ImGui.Selectable($"{_plugin.GetText($"settings.display.language.{language}")}##UiLanguage_{language}", isSelected))
        {
            _plugin.Config.UiLanguage = language;
            _plugin.ReloadLocalization();
            _plugin.SaveConfig();
        }

        if (isSelected)
            ImGui.SetItemDefaultFocus();
    }
}
