using StarLoom.Config;
using Xunit;

namespace StarLoom.Tests.Bootstrap;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void PluginConfig_Should_Exist()
    {
        _ = typeof(PluginConfig);
    }
}
