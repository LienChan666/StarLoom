using Starloom.UI.Components.Shared;
using Xunit;

namespace Starloom.Tests.UI;

public sealed class LayoutMetricsTests
{
    [Fact]
    public void CreateHome_ClampsLeftPaneWidthAndCalculatesHeights()
    {
        var metrics = LayoutMetrics.CreateHome(1200f, 800f, 12f);

        Assert.Equal(340f, metrics.LeftWidth);
        Assert.Equal(848f, metrics.RightWidth);
        Assert.Equal(346.72f, metrics.TopHeight, 2);
        Assert.Equal(441.28f, metrics.BottomHeight, 2);
    }

    [Fact]
    public void CreateHome_UsesMinimumLeftWidthAndKeepsBottomNonNegative()
    {
        var metrics = LayoutMetrics.CreateHome(400f, 180f, 12f);

        Assert.Equal(280f, metrics.LeftWidth);
        Assert.Equal(108f, metrics.RightWidth);
        Assert.Equal(190f, metrics.TopHeight);
        Assert.Equal(0f, metrics.BottomHeight);
    }

    [Fact]
    public void CreateSettings_ClampsSidebarWidth()
    {
        var narrow = LayoutMetrics.CreateSettings(800f, 12f);
        var wide = LayoutMetrics.CreateSettings(1400f, 12f);

        Assert.Equal(210f, narrow.NavigationWidth);
        Assert.Equal(578f, narrow.ContentWidth);
        Assert.Equal(250f, wide.NavigationWidth);
        Assert.Equal(1138f, wide.ContentWidth);
    }
}
