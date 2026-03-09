using Dalamud.Bindings.ImGui;
using Starloom.Data;
using Starloom.UI.Components.Shared;
using System;
using System.Linq;

namespace Starloom.UI.Components.Settings;

internal sealed class ShopSettingsCard
{
    public void Draw()
    {
        var currentShop = C.PreferredCollectableShop;
        var preview = currentShop?.Name ?? P.Localization.Get("common.not_selected");
        if (!GamePanelStyle.BeginSettingsTable("##ShopSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(P.Localization.Get("settings.shop.collectable_shop"));

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
                    C.PreferredCollectableShop = shop;
                    P.ConfigStore.Save();
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
