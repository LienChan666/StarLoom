using StarLoom.Ui.Components.Home;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class SelectedItemsOrderingTests
{
    [Fact]
    public void MoveUp_Should_Swap_With_Previous_Item()
    {
        var reordered = SelectedItemsOrdering.MoveUp([1, 2, 3], 1);

        Assert.Equal([2, 1, 3], reordered);
    }
}
