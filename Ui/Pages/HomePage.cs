using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Tasks;
using StarLoom.Tasks.Purchase;
using StarLoom.Ui.Components.Home;
using StarLoom.Ui.Components.Shared;
using StarLoom.Ui;
using System.Numerics;

namespace StarLoom.Ui.Pages;

public readonly record struct HomePageState(
    bool canStartConfiguredWorkflow,
    bool canStartTurnInOnly,
    bool canStartPurchaseOnly,
    bool canStop,
    bool showPurchaseRequirementHint)
{
    public static HomePageState FromWorkflow(bool isBusy, bool hasConfiguredPurchases)
    {
        return new HomePageState(
            canStartConfiguredWorkflow: !isBusy,
            canStartTurnInOnly: !isBusy,
            canStartPurchaseOnly: !isBusy && hasConfiguredPurchases,
            canStop: isBusy,
            showPurchaseRequirementHint: !isBusy && !hasConfiguredPurchases);
    }
}

public sealed class HomePage
{
    private const float PaneSpacing = 12f;
    private readonly HomeControlPane controlPane;
    private readonly SearchPane searchPane;
    private readonly SelectedItemsPane selectedItemsPane;

    public HomePage(WorkflowTask workflowTask, ConfigStore configStore, PurchaseCatalog purchaseCatalog, UiText uiText)
    {
        controlPane = new HomeControlPane(workflowTask, configStore, uiText);
        searchPane = new SearchPane(purchaseCatalog, configStore, uiText);
        selectedItemsPane = new SelectedItemsPane(configStore, uiText);
    }

    public void Draw()
    {
        var availableSize = ImGui.GetContentRegionAvail();
        var layout = LayoutMetrics.CreateHome(availableSize.X, availableSize.Y, PaneSpacing);

        controlPane.Draw(new Vector2(layout.LeftWidth, availableSize.Y));
        ImGui.SameLine(0f, PaneSpacing);

        if (!ImGui.BeginChild("##HomeContent", new Vector2(layout.RightWidth, availableSize.Y), true))
        {
            ImGui.EndChild();
            return;
        }

        searchPane.Draw(new Vector2(0f, layout.TopHeight));
        ImGui.Dummy(new Vector2(0f, PaneSpacing));
        selectedItemsPane.Draw(new Vector2(0f, layout.BottomHeight));
        ImGui.EndChild();
    }
}
