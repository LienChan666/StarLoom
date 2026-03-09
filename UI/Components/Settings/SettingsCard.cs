using Dalamud.Bindings.ImGui;
using StarLoom.UI.Components.Shared;
using System;

namespace StarLoom.UI.Components.Settings;

internal static class SettingsCard
{
    internal static void Draw(string id, string title, string description, Action drawContent)
    {
        ImGui.PushID(id);
        GamePanelStyle.DrawPanelHeader(title, description);
        drawContent();
        ImGui.PopID();
    }
}
