using StarLoom.Config;
using StarLoom.Game;
using StarLoom.Tasks.Purchase;
using Xunit;

namespace StarLoom.Tests.Tasks.Purchase;

public sealed class PurchasePlanTests
{
    [Fact]
    public void ResolvePendingTargets_Should_Subtract_Actual_Inventory_Count()
    {
        var pluginConfig = new PluginConfig
        {
            scripShopItems =
            [
                new PurchaseItemConfig
                {
                    itemId = 2001,
                    itemName = "Cordial",
                    targetCount = 20,
                    scripCost = 25,
                    page = "1",
                    subPage = "2",
                    currencyItemId = 33913,
                },
                new PurchaseItemConfig
                {
                    itemId = 2002,
                    itemName = "Already Done",
                    targetCount = 5,
                    scripCost = 10,
                    page = "3",
                    subPage = "1",
                    currencyItemId = 33913,
                },
            ],
        };

        var inventoryGame = new FakeInventoryGame();
        inventoryGame.itemCounts[2001] = 17;
        inventoryGame.itemCounts[2002] = 5;

        var resolver = new PendingPurchaseResolver(pluginConfig, inventoryGame);

        var pending = resolver.ResolvePendingTargets();

        var item = Assert.Single(pending);
        Assert.Equal(2001u, item.itemId);
        Assert.Equal("Cordial", item.itemName);
        Assert.Equal(3, item.remainingQuantity);
        Assert.Equal(25, item.itemCost);
        Assert.Equal("1", item.page);
        Assert.Equal("2", item.subPage);
        Assert.Equal(33913u, item.currencyItemId);
    }

    [Fact]
    public void ResolvePurchaseQuantity_Should_Use_Reserve_Amount_And_Cap_At_99()
    {
        var entry = new PurchaseEntry(
            3001,
            "Purple Gatherers' Scrip Token",
            250,
            2,
            "1",
            "1",
            33914);

        var quantity = PurchasePlan.ResolvePurchaseQuantity(
            entry,
            currentCurrencyCount: 250,
            reserveAmount: 10);

        Assert.Equal(99, quantity);
    }

    [Fact]
    public void SyncConfiguredItems_Should_Update_Configured_Metadata_When_Catalog_Changes()
    {
        var pluginConfig = new PluginConfig
        {
            scripShopItems =
            [
                new PurchaseItemConfig
                {
                    itemId = 4001,
                    itemName = "Old Name",
                    targetCount = 3,
                    scripCost = 25,
                    page = "1",
                    subPage = "1",
                    currencySpecialId = 2,
                    currencyItemId = 33913,
                    currencyName = "Old Currency",
                },
            ],
        };

        var saveCount = 0;
        var sync = new PurchaseCatalogSync(pluginConfig, () => saveCount++);

        sync.Apply(
            [
                new PurchaseCatalogItem(
                    4001,
                    "New Name",
                    50,
                    "3",
                    "4",
                    6,
                    41784,
                    "Purple Crafters' Scrip",
                    PurchaseDiscipline.Crafting,
                    7),
            ]);

        Assert.Equal(1, saveCount);
        var configuredItem = Assert.Single(pluginConfig.scripShopItems);
        Assert.Equal("New Name", configuredItem.itemName);
        Assert.Equal(50, configuredItem.scripCost);
        Assert.Equal("3", configuredItem.page);
        Assert.Equal("4", configuredItem.subPage);
        Assert.Equal((byte)6, configuredItem.currencySpecialId);
        Assert.Equal(41784u, configuredItem.currencyItemId);
        Assert.Equal("Purple Crafters' Scrip", configuredItem.currencyName);
    }

    private sealed class FakeInventoryGame : InventoryGame
    {
        public Dictionary<uint, int> itemCounts = [];

        public override int GetItemCount(uint itemId)
        {
            return itemCounts.TryGetValue(itemId, out var count) ? count : 0;
        }
    }
}
