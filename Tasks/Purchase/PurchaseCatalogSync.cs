using StarLoom.Config;

namespace StarLoom.Tasks.Purchase;

public sealed class PurchaseCatalogSync
{
    private readonly PluginConfig pluginConfig;
    private readonly Action saveConfig;

    public PurchaseCatalogSync(PluginConfig pluginConfig, Action saveConfig)
    {
        this.pluginConfig = pluginConfig;
        this.saveConfig = saveConfig;
    }

    public bool Apply(IReadOnlyList<PurchaseCatalogItem> latestItems)
    {
        if (pluginConfig.scripShopItems.Count == 0 || latestItems.Count == 0)
            return false;

        var latestLookup = latestItems.ToDictionary(item => item.itemId);
        var changed = false;

        foreach (var configuredItem in pluginConfig.scripShopItems)
        {
            if (!latestLookup.TryGetValue(configuredItem.itemId, out var latest))
                continue;

            if (AreEquivalent(configuredItem, latest))
                continue;

            configuredItem.itemName = latest.itemName;
            configuredItem.index = latest.index;
            configuredItem.scripCost = unchecked((int)latest.itemCost);
            configuredItem.page = latest.page;
            configuredItem.subPage = latest.subPage;
            configuredItem.currencySpecialId = latest.currencySpecialId;
            configuredItem.currencyItemId = latest.currencyItemId;
            configuredItem.currencyName = latest.currencyName;
            changed = true;
        }

        if (changed)
            saveConfig();

        return changed;
    }

    private static bool AreEquivalent(PurchaseItemConfig configuredItem, PurchaseCatalogItem latest)
    {
        return configuredItem.itemId == latest.itemId
            && string.Equals(configuredItem.itemName, latest.itemName, StringComparison.Ordinal)
            && configuredItem.index == latest.index
            && configuredItem.scripCost == unchecked((int)latest.itemCost)
            && string.Equals(configuredItem.page, latest.page, StringComparison.Ordinal)
            && string.Equals(configuredItem.subPage, latest.subPage, StringComparison.Ordinal)
            && configuredItem.currencySpecialId == latest.currencySpecialId
            && configuredItem.currencyItemId == latest.currencyItemId
            && string.Equals(configuredItem.currencyName, latest.currencyName, StringComparison.Ordinal);
    }
}
