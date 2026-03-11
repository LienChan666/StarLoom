using StarLoom.Ui.Components.Shared;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class LayoutMetricsTests
{
    [Fact]
    public void CreateHome_Should_Match_Original_Widths_And_Heights()
    {
        var metrics = LayoutMetrics.CreateHome(1200f, 800f, 12f);

        Assert.Equal(340f, metrics.LeftWidth);
        Assert.Equal(848f, metrics.RightWidth);
        Assert.Equal(346.72f, metrics.TopHeight, 2);
        Assert.Equal(441.28f, metrics.BottomHeight, 2);
    }

    [Fact]
    public void CreateSettings_Should_Use_Single_Pane_Content()
    {
        var narrow = LayoutMetrics.CreateSettings(800f, 12f);
        var wide = LayoutMetrics.CreateSettings(1400f, 12f);

        Assert.Equal(0f, narrow.NavigationWidth);
        Assert.Equal(800f, narrow.ContentWidth);
        Assert.Equal(0f, wide.NavigationWidth);
        Assert.Equal(1400f, wide.ContentWidth);
    }
}
