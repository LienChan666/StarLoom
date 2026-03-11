using StarLoom.Config;
using System.Reflection;
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
        Assert.Equal(10, pluginConfig.freeSlotThreshold);
    }

    [Fact]
    public void ReturnPointConfig_Should_Expose_A_Settable_Inn_Flag_And_Housing_Details()
    {
        var innFlag = typeof(ReturnPointConfig).GetProperty("isInn", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(innFlag);
        Assert.True(innFlag!.CanRead);
        Assert.True(innFlag.CanWrite);

        var apartmentFlag = typeof(ReturnPointConfig).GetProperty("isApartment", BindingFlags.Public | BindingFlags.Instance);
        var territoryId = typeof(ReturnPointConfig).GetProperty("territoryId", BindingFlags.Public | BindingFlags.Instance);
        var aetheryteId = typeof(ReturnPointConfig).GetProperty("aetheryteId", BindingFlags.Public | BindingFlags.Instance);
        var subIndex = typeof(ReturnPointConfig).GetProperty("subIndex", BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(typeof(bool), apartmentFlag?.PropertyType);
        Assert.Equal(typeof(uint), territoryId?.PropertyType);
        Assert.Equal(typeof(uint), aetheryteId?.PropertyType);
        Assert.Equal(typeof(byte), subIndex?.PropertyType);
    }
}
