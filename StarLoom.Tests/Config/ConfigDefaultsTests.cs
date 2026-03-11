using StarLoom.Config;
using Xunit;

namespace StarLoom.Tests.Config;

public sealed class ConfigDefaultsTests
{
    [Fact]
    public void Apply_Should_Set_Default_Return_Point_And_Language()
    {
        var pluginConfig = new PluginConfig();

        ConfigDefaults.Apply(pluginConfig);

        Assert.NotNull(pluginConfig.defaultReturnPoint);
        Assert.Equal("zh", pluginConfig.uiLanguage);
        Assert.Equal(PostPurchaseAction.ReturnToConfiguredPoint, pluginConfig.postPurchaseAction);
    }
}
