using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;

namespace StarLoom.Ui.Components.Settings;

internal sealed class DisplaySettingsCard
{
    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public DisplaySettingsCard(ConfigStore configStore, UiText uiText)
    {
        this.configStore = configStore;
        this.uiText = uiText;
    }

    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##DisplaySettingsTable"))
            return;

        var showStatusOverlay = configStore.pluginConfig.showStatusOverlay;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.display.overlay"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox($"{uiText.Get("settings.display.overlay_toggle")}##DisplayOverlay", ref showStatusOverlay))
        {
            configStore.pluginConfig.showStatusOverlay = showStatusOverlay;
            configStore.Save();
        }

        var uiLanguage = configStore.pluginConfig.uiLanguage;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.display.language"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##UiLanguage", uiText.Get($"settings.display.language.{uiLanguage}")))
        {
            DrawLanguageOption("zh");
            DrawLanguageOption("en");
            ImGui.EndCombo();
        }

        ImGui.EndTable();
    }

    private void DrawLanguageOption(string language)
    {
        var isSelected = string.Equals(configStore.pluginConfig.uiLanguage, language, StringComparison.Ordinal);
        if (ImGui.Selectable($"{uiText.Get($"settings.display.language.{language}")}##UiLanguage_{language}", isSelected))
        {
            configStore.pluginConfig.uiLanguage = language;
            uiText.Reload();
            configStore.Save();
        }

        if (isSelected)
            ImGui.SetItemDefaultFocus();
    }
}
