using Xunit;

namespace StarLoom.Tests;

public sealed class LoggingRemovalSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("Services/AutomationController.cs")]
    [InlineData("Core/JobOrchestrator.cs")]
    [InlineData("Services/ManagedArtisanSession.cs")]
    [InlineData("Data/ScripShopItemManager.cs")]
    [InlineData("Services/LocalizationService.cs")]
    public void Key_Runtime_Source_Uses_Dalamud_Log_Output(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, relativePath));

        Assert.Contains("Svc.Log.", source);
    }
}
