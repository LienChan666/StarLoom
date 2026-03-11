using StarLoom.Ui;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class UiTextTests
{
    [Fact]
    public void Get_Should_Load_English_Text_From_Resources()
    {
        var uiText = UiText.CreateForTests(AppContext.BaseDirectory, "en");

        Assert.Equal("Start", uiText.Get("common.start"));
    }
}
