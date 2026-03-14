using Dalamud.Interface.Windowing;
using System;

namespace Starloom.UI;

public sealed class PluginUi : IDisposable
{
    private readonly WindowSystem windowSystem;
    private readonly MainWindow mainWindow;
    private readonly StatusOverlay statusOverlay;

    public PluginUi()
    {
        windowSystem = new WindowSystem("Starloom");
        mainWindow = new MainWindow();
        statusOverlay = new StatusOverlay { IsOpen = C.ShowStatusOverlay };

        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(statusOverlay);

        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainWindow;
    }

    public void OpenMainWindow() => mainWindow.IsOpen = true;
    public void ToggleMainWindow() => mainWindow.IsOpen = !mainWindow.IsOpen;

    private void Draw()
    {
        if (statusOverlay.IsOpen != C.ShowStatusOverlay)
            statusOverlay.IsOpen = C.ShowStatusOverlay;

        windowSystem.Draw();

        if (C.ShowStatusOverlay != statusOverlay.IsOpen)
        {
            C.ShowStatusOverlay = statusOverlay.IsOpen;
            P.ConfigStore.Save();
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
