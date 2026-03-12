using StarLoom.Tasks.Artisan;
using Xunit;

namespace StarLoom.Tests.Tasks.Artisan;

public sealed class ArtisanSnapshotTests
{
    [Fact]
    public void IsReady_Should_Be_True_When_Ipc_Is_Available_And_List_Id_Is_Valid()
    {
        var artisanSnapshot = new ArtisanSnapshot(true, true, false, false, false, false, 7);

        Assert.True(artisanSnapshot.isReady);
    }
}
