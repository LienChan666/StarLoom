using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;
using System.Numerics;

namespace StarLoom.Ui.Components.Settings;

internal sealed class ShopSettingsCard
{
    private static readonly IReadOnlyList<CollectableShopConfig> CollectableShops =
    [
        new CollectableShopConfig
        {
            displayName = "Solution Nine",
            location = new Vector3(-162.17f, 0.9219f, -30.458f),
            scripShopLocation = new Vector3(-161.84605f, 0.921f, -42.06536f),
            aetheryteId = 186,
            territoryId = 1186,
            npcId = 1027542,
            scripShopNpcId = 1027541,
            isLifestreamRequired = true,
            lifestreamCommand = "Nexus Arcade",
        },
        new CollectableShopConfig
        {
            displayName = "Eulmore",
            location = new Vector3(16.94f, 82.05f, -19.177f),
            scripShopLocation = new Vector3(16.94f, 82.05f, -19.177f),
            aetheryteId = 134,
            territoryId = 820,
            npcId = 1027542,
            scripShopNpcId = 1027541,
        },
        new CollectableShopConfig
        {
            displayName = "Old Gridania",
            location = new Vector3(143.62454f, 13.74769f, -105.33799f),
            scripShopLocation = new Vector3(143.62454f, 13.74769f, -105.33799f),
            aetheryteId = 2,
            territoryId = 133,
            npcId = 1027542,
            scripShopNpcId = 1027541,
            isLifestreamRequired = true,
            lifestreamCommand = "Leatherworkers",
        },
    ];

    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public ShopSettingsCard(ConfigStore configStore, UiText uiText)
    {
        this.configStore = configStore;
        this.uiText = uiText;
    }

    public void Draw()
    {
        var currentShop = configStore.pluginConfig.preferredCollectableShop;
        var preview = currentShop?.displayName ?? uiText.Get("common.not_selected");
        if (!GamePanelStyle.BeginSettingsTable("##ShopSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.shop.collectable_shop"));

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(320f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##PreferredCollectableShop", preview))
        {
            foreach (var shop in CollectableShops)
            {
                var isSelected = currentShop != null
                    && string.Equals(currentShop.displayName, shop.displayName, StringComparison.Ordinal);
                if (ImGui.Selectable($"{shop.displayName}##Shop_{shop.displayName}", isSelected))
                {
                    configStore.pluginConfig.preferredCollectableShop = CloneShop(shop);
                    configStore.Save();
                    preview = shop.displayName;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.EndTable();
    }

    private static CollectableShopConfig CloneShop(CollectableShopConfig shop)
    {
        return new CollectableShopConfig
        {
            location = shop.location,
            displayName = shop.displayName,
            territoryId = shop.territoryId,
            aetheryteId = shop.aetheryteId,
            npcId = shop.npcId,
            scripShopNpcId = shop.scripShopNpcId,
            scripShopLocation = shop.scripShopLocation,
            isLifestreamRequired = shop.isLifestreamRequired,
            lifestreamCommand = shop.lifestreamCommand,
        };
    }
}
