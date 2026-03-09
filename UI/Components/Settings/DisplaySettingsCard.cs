using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;

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
        GamePanelStyle.DrawSettingLabel("状态悬浮窗");
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox("显示悬浮窗", ref showStatusOverlay))
            _pluginUi.SetStatusOverlayVisible(showStatusOverlay);

        ImGui.EndTable();
    }
}
