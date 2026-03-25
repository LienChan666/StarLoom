using Dalamud.Bindings.ImGui;
using Starloom.UI.Components.Settings;
using Starloom.UI.Components.Shared;
using System.Numerics;

namespace Starloom.UI.Components.Home;

internal sealed class HomeTab
{
    private const float PaneSpacing = 12f;

    private readonly HomeControlPane controlPane = new();
    private readonly ShopSettingsCard shopSettingsCard = new();
    private readonly CraftPointSettingsCard craftPointSettingsCard = new();
    private readonly PurchaseSettingsCard purchaseSettingsCard = new();
    private readonly DisplaySettingsCard displaySettingsCard = new();
    private readonly SearchPane searchPane = new();
    private readonly SelectedItemsPane selectedItemsPane = new();

    public void Draw()
    {
        var availableSize = ImGui.GetContentRegionAvail();
        var layout = LayoutMetrics.CreateHome(availableSize.X, availableSize.Y, PaneSpacing);

        if (!ImGui.BeginChild("##HomeSidebar", new Vector2(layout.LeftWidth, availableSize.Y), true))
        {
            ImGui.EndChild();
            return;
        }

        controlPane.Draw();
        ImGui.Separator();
        shopSettingsCard.Draw();
        ImGui.Separator();
        craftPointSettingsCard.Draw();
        ImGui.Separator();
        purchaseSettingsCard.Draw();
        ImGui.Separator();
        displaySettingsCard.Draw();
        ImGui.EndChild();

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
