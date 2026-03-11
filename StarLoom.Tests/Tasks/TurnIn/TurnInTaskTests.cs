using StarLoom.Game;
using StarLoom.Tasks.TurnIn;
using Xunit;

namespace StarLoom.Tests.Tasks.TurnIn;

public sealed class TurnInTaskTests
{
    [Fact]
    public void Start_Should_Build_Queue_From_Live_Inventory_And_Resolve_JobId()
    {
        var inventoryGame = new FakeInventoryGame
        {
            items =
            [
                new InventoryItemView(1001, true, 2, "Rarefied Tea"),
                new InventoryItemView(1002, false, 4, "Normal Tea"),
                new InventoryItemView(1003, true, 1, "Rarefied Soup"),
            ],
            eligibleItemIds = [1001, 1003],
        };

        var resolver = new FakeTurnInJobResolver();
        resolver.jobs[1001] = 8;
        resolver.jobs[1003] = 15;
        var turnInTask = new TurnInTask(inventoryGame, new FakeCollectableShopGame(), resolver);

        turnInTask.Start();

        var queue = turnInTask.GetQueue();
        Assert.Equal(2, queue.Count);
        Assert.Equal(1001u, queue[0].itemId);
        Assert.Equal(8u, queue[0].jobId);
        Assert.Equal(1003u, queue[1].itemId);
        Assert.Equal(15u, queue[1].jobId);
    }

    [Fact]
    public void Update_Should_Wait_For_Collectable_Window_And_Only_Complete_After_Inventory_Drops()
    {
        var inventoryGame = new FakeInventoryGame
        {
            collectableCounts = { [1001] = 1 },
        };

        var collectableShopGame = new FakeCollectableShopGame();
        var now = DateTime.UnixEpoch;
        var turnInTask = new TurnInTask(
            inventoryGame,
            collectableShopGame,
            new FakeTurnInJobResolver(),
            () => now);

        turnInTask.Start([new TurnInCandidate(1001, "Rarefied Tea", 1, true, 8)]);

        turnInTask.Update();
        Assert.Equal("WaitingForCollectableShop", turnInTask.currentStage);

        collectableShopGame.isReady = true;
        turnInTask.Update();
        now += TimeSpan.FromSeconds(1);
        turnInTask.Update();
        now += TimeSpan.FromSeconds(1);
        turnInTask.Update();
        now += TimeSpan.FromSeconds(1);
        turnInTask.Update();
        now += TimeSpan.FromMilliseconds(100);
        turnInTask.Update();

        Assert.False(turnInTask.isCompleted);
        Assert.Equal(1, collectableShopGame.submitCount);

        inventoryGame.collectableCounts[1001] = 0;
        now += TimeSpan.FromSeconds(1);
        turnInTask.Update();
        turnInTask.Update();

        Assert.True(turnInTask.isCompleted);
        Assert.False(turnInTask.hasFailed);
        Assert.Equal(1, collectableShopGame.closeWindowCount);
    }

    [Fact]
    public void Update_Should_Dismiss_Overcap_Dialog_And_Cleanup()
    {
        var inventoryGame = new FakeInventoryGame
        {
            collectableCounts = { [1001] = 1 },
        };

        var collectableShopGame = new FakeCollectableShopGame
        {
            isReady = true,
            dismissOvercap = true,
        };

        var now = DateTime.UnixEpoch;
        var turnInTask = new TurnInTask(
            inventoryGame,
            collectableShopGame,
            new FakeTurnInJobResolver(),
            () => now);

        turnInTask.Start([new TurnInCandidate(1001, "Rarefied Tea", 1, true, 8)]);

        turnInTask.Update();
        now += TimeSpan.FromSeconds(1);
        turnInTask.Update();
        now += TimeSpan.FromSeconds(1);
        turnInTask.Update();
        now += TimeSpan.FromSeconds(1);
        turnInTask.Update();
        now += TimeSpan.FromMilliseconds(100);
        turnInTask.Update();
        turnInTask.Update();

        Assert.True(turnInTask.isCompleted);
        Assert.False(turnInTask.hasFailed);
        Assert.Equal(1, collectableShopGame.dismissOvercapCount);
        Assert.Equal(1, collectableShopGame.closeWindowCount);
    }

    private sealed class FakeInventoryGame : InventoryGame
    {
        public List<InventoryItemView> items = [];
        public HashSet<uint> eligibleItemIds = [];
        public Dictionary<uint, int> collectableCounts = [];

        public override List<InventoryItemView> GetInventoryItems()
        {
            return items;
        }

        public override bool IsCollectableTurnInItem(uint itemId)
        {
            return eligibleItemIds.Contains(itemId);
        }

        public override int GetCollectableInventoryItemCount(uint itemId)
        {
            return collectableCounts.TryGetValue(itemId, out var count) ? count : 0;
        }

        public override string GetItemName(uint itemId)
        {
            return items.FirstOrDefault(item => item.itemId == itemId).itemName;
        }

        public override void InvalidateTransientCaches()
        {
        }
    }

    private sealed class FakeCollectableShopGame : CollectableShopGame
    {
        public bool isReady;
        public bool dismissOvercap;
        public int submitCount;
        public int closeWindowCount;
        public int dismissOvercapCount;
        public int selectJobCount;
        public int selectItemCount;

        public override bool IsReady()
        {
            return isReady;
        }

        public override void SelectJob(uint jobId)
        {
            selectJobCount++;
        }

        public override void SelectItemById(uint itemId)
        {
            selectItemCount++;
        }

        public override void SubmitItem()
        {
            submitCount++;
        }

        public override bool TryDismissOvercapDialog()
        {
            if (!dismissOvercap)
                return false;

            dismissOvercapCount++;
            dismissOvercap = false;
            return true;
        }

        public override void CloseWindow()
        {
            closeWindowCount++;
        }
    }

    private sealed class FakeTurnInJobResolver : TurnInJobResolver
    {
        public Dictionary<uint, uint> jobs = [];

        public override uint ResolveJobId(uint itemId, string itemName)
        {
            return jobs.TryGetValue(itemId, out var jobId) ? jobId : 8;
        }
    }
}
