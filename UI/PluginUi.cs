using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using System;

namespace StarLoom.UI;

public sealed class PluginUi : IDisposable
{
    private readonly IPluginUiFacade _ui;
    private readonly WindowSystem _windowSystem;
    private readonly MainWindow _mainWindow;
    private readonly StatusOverlay _statusOverlay;

    public bool IsStatusOverlayVisible => _statusOverlay.IsOpen;

    public PluginUi(IPluginUiFacade ui)
    {
        _ui = ui;
        _windowSystem = new WindowSystem("Starloom");
        _mainWindow = new MainWindow(ui);
        _statusOverlay = new StatusOverlay(ui)
        {
            IsOpen = ui.Config.ShowStatusOverlay,
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
        if (_ui.Config.ShowStatusOverlay == isVisible)
            return;

        _ui.Config.ShowStatusOverlay = isVisible;
        _ui.SaveConfig();
    }

    private void Draw()
    {
        if (_statusOverlay.IsOpen != _ui.Config.ShowStatusOverlay)
            _statusOverlay.IsOpen = _ui.Config.ShowStatusOverlay;

        _windowSystem.Draw();

        if (_ui.Config.ShowStatusOverlay == _statusOverlay.IsOpen)
            return;

        _ui.Config.ShowStatusOverlay = _statusOverlay.IsOpen;
        _ui.SaveConfig();
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenMainWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMainWindow;
        _windowSystem.RemoveAllWindows();
    }
}

