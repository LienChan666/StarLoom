using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;

namespace StarLoom.Ui.Components.Settings;

internal sealed class ShopSettingsCard
{
    private static readonly IReadOnlyList<CollectableShopConfig> CollectableShops =
    [
        new CollectableShopConfig
        {
            displayName = "Solution Nine",
            aetheryteId = 186,
            territoryId = 1186,
            npcId = 1027542,
        },
        new CollectableShopConfig
        {
            displayName = "Eulmore",
            aetheryteId = 134,
            territoryId = 820,
            npcId = 1027542,
        },
        new CollectableShopConfig
        {
            displayName = "Old Gridania",
            aetheryteId = 2,
            territoryId = 133,
            npcId = 1027542,
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
            displayName = shop.displayName,
            territoryId = shop.territoryId,
            aetheryteId = shop.aetheryteId,
            npcId = shop.npcId,
        };
    }
}
