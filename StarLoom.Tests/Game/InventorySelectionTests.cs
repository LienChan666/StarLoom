using StarLoom.Game;
using Xunit;

namespace StarLoom.Tests.Game;

public sealed class InventorySelectionTests
{
    [Fact]
    public void Collectables_Should_Group_By_Item_Id()
    {
        var items = new[]
        {
            new InventoryItemView(1001, true, 2),
            new InventoryItemView(1001, true, 1),
            new InventoryItemView(1002, false, 4),
        };

        var grouped = InventoryGame.GroupCollectables(items);

        Assert.Single(grouped);
        Assert.Equal(3, grouped[0].quantity);
    }
}
