using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StarLoom.Config;
using StarLoom.Tasks;
using StarLoom.Tasks.Purchase;
using StarLoom.Ui.Pages;
using System.Numerics;

namespace StarLoom.Ui;

public sealed class MainWindow : Window
{
    private readonly HomePage homePage;
    private readonly SettingsPage settingsPage;
    private readonly UiText uiText;

    public MainWindow(WorkflowTask workflowTask, ConfigStore configStore, PurchaseCatalog purchaseCatalog, UiText uiText)
        : base("StarLoom###StarLoomMainWindow")
    {
        this.uiText = uiText;
        homePage = new HomePage(workflowTask, configStore, purchaseCatalog, uiText);
        settingsPage = new SettingsPage(configStore, uiText);
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new Vector2(1180f, 760f), ImGuiCond.FirstUseEver);
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##StarLoomTabs"))
            return;

        if (ImGui.BeginTabItem(uiText.Get("main.tab.home")))
        {
            homePage.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(uiText.Get("main.tab.settings")))
        {
            settingsPage.Draw();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
