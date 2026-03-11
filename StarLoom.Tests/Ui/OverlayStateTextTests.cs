using StarLoom.Ui;
using System.IO;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class OverlayStateTextTests
{
    [Fact]
    public void Overlay_Title_Key_Should_Exist()
    {
        Assert.Contains("overlay.total_state", UiLocalizationKeys.Required);
    }

    [Fact]
    public void Required_Localization_Keys_Should_Cover_Workflow_State_Text()
    {
        Assert.Contains("state.orchestrator.idle", UiLocalizationKeys.Required);
        Assert.Contains("state.orchestrator.waiting_pause", UiLocalizationKeys.Required);
        Assert.Contains("state.orchestrator.waiting_idle", UiLocalizationKeys.Required);
        Assert.Contains("state.orchestrator.running", UiLocalizationKeys.Required);
        Assert.Contains("state.session.monitoring", UiLocalizationKeys.Required);
    }

    [Fact]
    public void Diff_Checklist_Should_Record_All_36_Items_As_Closed()
    {
        var checklistPath = Path.Combine(GetRepositoryRoot(), "docs", "reviews", "2026-03-11-execution-route-diff-checklist.md");
        var content = File.ReadAllText(checklistPath);

        for (var itemNumber = 1; itemNumber <= 36; itemNumber++)
            Assert.Contains($"- [x] {itemNumber}.", content);

        Assert.DoesNotContain("- [ ]", content);
        Assert.Contains("Execution-route parity has converged.", content);
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
