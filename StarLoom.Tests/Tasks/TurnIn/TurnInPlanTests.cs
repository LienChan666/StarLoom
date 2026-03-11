using StarLoom.Tasks.TurnIn;
using Xunit;

namespace StarLoom.Tests.Tasks.TurnIn;

public sealed class TurnInPlanTests
{
    [Fact]
    public void BuildQueue_Should_Ignore_NonCollectables_And_Unresolved_Jobs()
    {
        var queue = TurnInPlan.BuildQueue(
        [
            new TurnInCandidate(1001, "A", 2, true, 8),
            new TurnInCandidate(1002, "B", 3, false, 9),
            new TurnInCandidate(1003, "C", 1, true, 0),
        ]);

        Assert.Single(queue);
        Assert.Equal(1001u, queue[0].itemId);
        Assert.Equal(8u, queue[0].jobId);
    }

    [Fact]
    public void BuildQueue_Should_Group_Duplicate_Candidates_And_Preserve_JobId()
    {
        var queue = TurnInPlan.BuildQueue(
        [
            new TurnInCandidate(1001, "Rarefied A", 2, true, 8),
            new TurnInCandidate(1001, "Rarefied A", 1, true, 8),
        ]);

        Assert.Single(queue);
        Assert.Equal(3, queue[0].quantity);
        Assert.Equal(8u, queue[0].jobId);
    }
}
