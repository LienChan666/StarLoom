using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using System;

namespace Starloom.UI;

public sealed class PluginUi : IDisposable
{
    private readonly Plugin _plugin;
    private readonly WindowSystem _windowSystem;
    private readonly MainWindow _mainWindow;
    private readonly StatusOverlay _statusOverlay;

    public bool IsStatusOverlayVisible => _statusOverlay.IsOpen;

    public PluginUi(Plugin plugin)
    {
        _plugin = plugin;
        _windowSystem = new WindowSystem("Starloom");
        _mainWindow = new MainWindow(plugin, this);
        _statusOverlay = new StatusOverlay(plugin)
        {
            IsOpen = plugin.Config.ShowStatusOverlay,
        };

        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_statusOverlay);

        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainWindow;
    }

    public void OpenMainWindow()
        => _mainWindow.IsOpen = true;

    public void ToggleMainWindow()
        => _mainWindow.IsOpen = !_mainWindow.IsOpen;

    public void ToggleStatusOverlay()
        => SetStatusOverlayVisible(!_statusOverlay.IsOpen);

    public void SetStatusOverlayVisible(bool isVisible)
    {
        _statusOverlay.IsOpen = isVisible;
        if (_plugin.Config.ShowStatusOverlay == isVisible)
            return;

        _plugin.Config.ShowStatusOverlay = isVisible;
        _plugin.SaveConfig();
    }

    private void Draw()
    {
        _windowSystem.Draw();

        if (_plugin.Config.ShowStatusOverlay == _statusOverlay.IsOpen)
            return;

        _plugin.Config.ShowStatusOverlay = _statusOverlay.IsOpen;
        _plugin.SaveConfig();
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMainWindow;
        _windowSystem.RemoveAllWindows();
    }
}
