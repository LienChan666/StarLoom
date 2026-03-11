using StarLoom.Ui.Components.Settings;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class SettingsInputClampTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 5)]
    public void ClampNonNegative_Should_Return_Expected_Value(int input, int expected)
    {
        Assert.Equal(expected, SettingsValueRules.ClampNonNegative(input));
    }
}
