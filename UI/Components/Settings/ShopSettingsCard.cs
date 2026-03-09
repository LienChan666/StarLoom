using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System;
using System.Linq;

namespace StarLoom.UI.Components.Settings;

internal sealed class ShopSettingsCard
{
    private readonly IPluginUiFacade _ui;

    public ShopSettingsCard(IPluginUiFacade ui)
    {
        _ui = ui;
    }

    public void Draw()
    {
        var currentShop = _ui.Config.PreferredCollectableShop;
        var preview = currentShop?.Name ?? _ui.GetText("common.not_selected");
        if (!GamePanelStyle.BeginSettingsTable("##ShopSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.shop.collectable_shop"));

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
                    _ui.Config.PreferredCollectableShop = shop;
                    _ui.SaveConfig();
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

