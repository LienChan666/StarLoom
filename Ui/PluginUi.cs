using Dalamud.Interface.Windowing;
using StarLoom.Config;
using StarLoom.Tasks;
using StarLoom.Tasks.Purchase;

namespace StarLoom.Ui;

public sealed class PluginUi : IDisposable
{
    private readonly WindowSystem windowSystem;
    private readonly MainWindow mainWindow;
    private readonly StatusOverlay statusOverlay;
    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public PluginUi(WorkflowTask workflowTask, ConfigStore configStore, PurchaseCatalog purchaseCatalog)
    {
        this.configStore = configStore;
        uiText = new UiText(configStore);
        windowSystem = new WindowSystem("StarLoom");
        mainWindow = new MainWindow(workflowTask, configStore, purchaseCatalog, uiText);
        statusOverlay = new StatusOverlay(workflowTask, configStore, uiText)
        {
            IsOpen = configStore.pluginConfig.showStatusOverlay,
        };

        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(statusOverlay);

        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainWindow;
    }

    public void OpenMainWindow()
    {
        mainWindow.IsOpen = true;
    }

    public void ToggleMainWindow()
    {
        mainWindow.IsOpen = !mainWindow.IsOpen;
    }

    private void Draw()
    {
        if (statusOverlay.IsOpen != configStore.pluginConfig.showStatusOverlay)
            statusOverlay.IsOpen = configStore.pluginConfig.showStatusOverlay;

        windowSystem.Draw();

        if (configStore.pluginConfig.showStatusOverlay != statusOverlay.IsOpen)
        {
            configStore.pluginConfig.showStatusOverlay = statusOverlay.IsOpen;
            configStore.Save();
        }
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMainWindow;
        windowSystem.RemoveAllWindows();
    }
}
