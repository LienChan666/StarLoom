using StarLoom.Config;
using StarLoom.Game;
using StarLoom.Tasks.Purchase;
using Xunit;

namespace StarLoom.Tests.Tasks.Purchase;

public sealed class PurchaseTaskTests
{
    [Fact]
    public void Start_Should_Build_Queue_From_Pending_Targets()
    {
        var pluginConfig = CreateConfig(targetCount: 4, reserveAmount: 10);
        var inventoryGame = new FakeInventoryGame
        {
            itemCounts =
            {
                [100] = 1,
                [33913] = 40,
            },
        };

        var purchaseTask = new PurchaseTask(
            pluginConfig,
            inventoryGame,
            new FakeNpcGame(),
            new FakeScripShopGame(),
            new PendingPurchaseResolver(pluginConfig, inventoryGame),
            () => DateTime.UnixEpoch);

        purchaseTask.Start();

        var entry = Assert.Single(purchaseTask.GetQueue());
        Assert.Equal(100u, entry.itemId);
        Assert.Equal(3, entry.quantity);
        Assert.Equal(33913u, entry.currencyItemId);
    }

    [Fact]
    public void Update_Should_Use_Live_Currency_Reserve_And_Wait_For_Inventory_Change_Before_Completing()
    {
        var pluginConfig = CreateConfig(targetCount: 4, reserveAmount: 10);
        var inventoryGame = new FakeInventoryGame
        {
            itemCounts =
            {
                [100] = 1,
                [33913] = 40,
            },
        };

        var npcGame = new FakeNpcGame();
        var scripShopGame = new FakeScripShopGame();
        var now = DateTime.UnixEpoch;
        var purchaseTask = new PurchaseTask(
            pluginConfig,
            inventoryGame,
            npcGame,
            scripShopGame,
            new PendingPurchaseResolver(pluginConfig, inventoryGame),
            () => now);

        purchaseTask.Start();

        purchaseTask.Update();
        Assert.Contains(202u, npcGame.interactedNpcIds);

        scripShopGame.canOpenShop = true;
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();

        scripShopGame.isReady = true;
        purchaseTask.Update();

        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();

        Assert.Equal([3], scripShopGame.selectedAmounts);
        Assert.False(purchaseTask.isCompleted);
        Assert.Equal(1, scripShopGame.confirmPurchaseCount);

        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();
        Assert.False(purchaseTask.isCompleted);

        inventoryGame.itemCounts[100] = 4;
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();
        purchaseTask.Update();

        Assert.True(purchaseTask.isCompleted);
        Assert.False(purchaseTask.hasFailed);
        Assert.Equal(1, scripShopGame.closeShopCount);
    }

    [Fact]
    public void Update_Should_Cap_Per_Transaction_Quantity_At_99()
    {
        var pluginConfig = CreateConfig(targetCount: 150, reserveAmount: 0);
        var inventoryGame = new FakeInventoryGame
        {
            itemCounts =
            {
                [100] = 0,
                [33913] = 2000,
            },
        };

        var scripShopGame = new FakeScripShopGame
        {
            isReady = true,
        };

        var now = DateTime.UnixEpoch;
        var purchaseTask = new PurchaseTask(
            pluginConfig,
            inventoryGame,
            new FakeNpcGame(),
            scripShopGame,
            new PendingPurchaseResolver(pluginConfig, inventoryGame),
            () => now);

        purchaseTask.Start();

        purchaseTask.Update();
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();
        now += TimeSpan.FromSeconds(1);
        purchaseTask.Update();

        Assert.Equal([99], scripShopGame.selectedAmounts);
    }

    private static PluginConfig CreateConfig(int targetCount, int reserveAmount)
    {
        return new PluginConfig
        {
            reserveScripAmount = reserveAmount,
            preferredCollectableShop = new CollectableShopConfig
            {
                scripShopNpcId = 202,
            },
            scripShopItems =
            [
                new PurchaseItemConfig
                {
                    itemId = 100,
                    itemName = "Token",
                    targetCount = targetCount,
                    scripCost = 10,
                    page = "1",
                    subPage = "2",
                    currencyItemId = 33913,
                },
            ],
        };
    }

    private sealed class FakeInventoryGame : InventoryGame
    {
        public Dictionary<uint, int> itemCounts = [];

        public override int GetItemCount(uint itemId)
        {
            return itemCounts.TryGetValue(itemId, out var count) ? count : 0;
        }

        public override int GetCurrencyCount(uint itemId)
        {
            return itemCounts.TryGetValue(itemId, out var count) ? count : 0;
        }

        public override void InvalidateTransientCaches()
        {
        }
    }

    private sealed class FakeNpcGame : NpcGame
    {
        public List<uint> interactedNpcIds = [];

        public override bool TryInteract(uint npcId, float maxDistance = 6f)
        {
            interactedNpcIds.Add(npcId);
            return true;
        }
    }

    private sealed class FakeScripShopGame : ScripShopGame
    {
        public bool isReady;
        public bool canOpenShop;
        public int confirmPurchaseCount;
        public int closeShopCount;
        public List<int> selectedAmounts = [];

        public override bool IsReady()
        {
            return isReady;
        }

        public override bool OpenShop()
        {
            return canOpenShop;
        }

        public override void SelectPage(string page)
        {
        }

        public override void SelectSubPage(string subPage)
        {
        }

        public override bool SelectItem(uint itemId, string itemName, int amount)
        {
            selectedAmounts.Add(amount);
            return true;
        }

        public override PurchaseDialogResult ConfirmPurchase(uint itemId, string itemName)
        {
            confirmPurchaseCount++;
            return PurchaseDialogResult.Confirmed;
        }

        public override void CloseShop()
        {
            closeShopCount++;
        }
    }
}
