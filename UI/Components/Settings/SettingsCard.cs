using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Settings;

internal static class SettingsCard
{
    internal static void Draw(string id, string title, string description, Action drawContent)
    {
        ImGui.PushID(id);
        GamePanelStyle.DrawPanelHeader(title, description);
        ImGui.Dummy(new Vector2(0, GamePanelStyle.Spacing.Xs));
        drawContent();
        ImGui.PopID();
    }
}
