using Dalamud.Interface.Windowing;
using System;

namespace Starloom.UI;

public sealed class PluginUi : IDisposable
{
    private readonly WindowSystem windowSystem;
    private readonly MainWindow mainWindow;

    public PluginUi()
    {
        windowSystem = new WindowSystem("Starloom");
        mainWindow = new MainWindow();

        windowSystem.AddWindow(mainWindow);

        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainWindow;
    }

    public void OpenMainWindow() => mainWindow.IsOpen = true;
    public void ToggleMainWindow() => mainWindow.IsOpen = !mainWindow.IsOpen;

    private void Draw()
        => windowSystem.Draw();

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMainWindow;
        windowSystem.RemoveAllWindows();
    }
}
