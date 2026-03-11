using StarLoom.Ui;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class OverlayStateTextTests
{
    [Fact]
    public void Overlay_Title_Key_Should_Exist()
    {
        Assert.Contains("overlay.total_state", UiLocalizationKeys.Required);
    }
}
