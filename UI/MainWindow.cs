using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Starloom.UI.Components.Home;
using System.Numerics;

namespace Starloom.UI;

public sealed class MainWindow : Window
{
    private readonly HomeTab homeTab;

    public MainWindow() : base("Starloom###StarloomMainWindow")
    {
        homeTab = new HomeTab();
    }

    public override void PreDraw()
        => ImGui.SetNextWindowSize(new Vector2(1180, 760), ImGuiCond.FirstUseEver);

    public override void Draw()
        => homeTab.Draw();
}
