using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.UI.Components.Shared;
using System;
using System.Linq;

namespace StarLoom.UI.Components.Settings;

internal sealed class ShopSettingsCard
{
    private readonly Plugin _plugin;

    public ShopSettingsCard(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        var currentShop = _plugin.Config.PreferredCollectableShop;
        var preview = currentShop?.Name ?? _plugin.GetText("common.not_selected");
        if (!GamePanelStyle.BeginSettingsTable("##ShopSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_plugin.GetText("settings.shop.collectable_shop"));

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(320f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##PreferredCollectableShop", preview))
        {
            foreach (var shop in NpcLocations.CollectableShops.Where(static shop => !shop.Disabled))
            {
                var isSelected = currentShop != null
                    && string.Equals(currentShop.Name, shop.Name, StringComparison.Ordinal);
                if (ImGui.Selectable($"{shop.Name}##Shop_{shop.Name}", isSelected))
                {
                    _plugin.Config.PreferredCollectableShop = shop;
                    _plugin.SaveConfig();
                    currentShop = shop;
                    preview = currentShop.Name;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.EndTable();
    }
}
