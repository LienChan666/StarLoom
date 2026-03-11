using StarLoom.Tasks.Purchase;
using Xunit;

namespace StarLoom.Tests.Tasks.Purchase;

public sealed class PurchasePlanTests
{
    [Fact]
    public void BuildQueue_Should_Respect_Reserve_Amount()
    {
        var queue = PurchasePlan.BuildQueue(
            [new PurchaseTarget(2001, "Cordial", 20, 10, 999)],
            currentScrips: 205,
            reserveAmount: 200);

        Assert.Empty(queue);
    }
}
