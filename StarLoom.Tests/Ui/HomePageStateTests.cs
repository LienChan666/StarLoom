using StarLoom.Ui.Pages;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class HomePageStateTests
{
    [Fact]
    public void Start_Button_Should_Disable_When_Workflow_Is_Busy()
    {
        var state = HomePageState.FromWorkflow(isBusy: true, hasConfiguredPurchases: true);

        Assert.False(state.canStartConfiguredWorkflow);
    }

    [Fact]
    public void Purchase_Hint_Should_Show_When_No_Configured_Items_Exist()
    {
        var state = HomePageState.FromWorkflow(isBusy: false, hasConfiguredPurchases: false);

        Assert.True(state.showPurchaseRequirementHint);
    }
}
