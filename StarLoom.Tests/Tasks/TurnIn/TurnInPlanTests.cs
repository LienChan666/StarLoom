using StarLoom.Tasks.TurnIn;
using Xunit;

namespace StarLoom.Tests.Tasks.TurnIn;

public sealed class TurnInPlanTests
{
    [Fact]
    public void BuildQueue_Should_Ignore_NonCollectables()
    {
        var queue = TurnInPlan.BuildQueue(
            [new TurnInCandidate(1001, "A", 2, true), new TurnInCandidate(1002, "B", 3, false)]);

        Assert.Single(queue);
        Assert.Equal(1001u, queue[0].itemId);
    }
}
