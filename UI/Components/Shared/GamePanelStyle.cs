using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Shared;

internal static class GamePanelStyle
{
    internal static readonly Vector4 PanelBackground = new(0.082f, 0.102f, 0.137f, 0.98f);
    internal static readonly Vector4 PanelBackgroundAlt = new(0.109f, 0.137f, 0.180f, 0.98f);
    internal static readonly Vector4 PanelBorder = new(0.251f, 0.396f, 0.475f, 0.95f);
    internal static readonly Vector4 Accent = new(0.380f, 0.769f, 0.847f, 1f);
    internal static readonly Vector4 AccentSoft = new(0.251f, 0.529f, 0.600f, 1f);
    internal static readonly Vector4 MutedText = new(0.670f, 0.729f, 0.800f, 1f);
    internal static readonly Vector4 Success = new(0.471f, 0.831f, 0.616f, 1f);
    internal static readonly Vector4 Warning = new(0.918f, 0.745f, 0.376f, 1f);
    internal static readonly Vector4 Danger = new(0.922f, 0.443f, 0.420f, 1f);

    internal static PanelScope BeginPanel(string id, Vector2 size, Vector4? borderColor = null)
        => new(id, size, borderColor ?? PanelBorder);

    internal static Vector4 Tint(Vector4 color, float factor)
        => new(
            Math.Clamp(color.X * factor, 0f, 1f),
            Math.Clamp(color.Y * factor, 0f, 1f),
            Math.Clamp(color.Z * factor, 0f, 1f),
            color.W);

    internal static void DrawPanelHeader(string title, string description)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Accent);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        if (!string.IsNullOrWhiteSpace(description))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
            ImGui.TextWrapped(description);
            ImGui.PopStyleColor();
        }

        ImGui.Separator();
    }

    internal static void DrawSectionTitle(string title, string description)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Accent);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        if (!string.IsNullOrWhiteSpace(description))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
            ImGui.TextWrapped(description);
            ImGui.PopStyleColor();
        }
    }

    internal static void DrawInfoRow(string label, string value)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();

        ImGui.SameLine(130f);
        ImGui.TextWrapped(value);
    }

    internal static void DrawHint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    internal static void DrawInlineBadge(string label, string value, Vector4 accent)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Tint(accent, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Tint(accent, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Tint(accent, 0.65f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.93f, 0.96f, 0.99f, 1f));
        ImGui.Button($"{label} · {value}");
        ImGui.PopStyleColor(4);
    }

    internal static bool BeginSettingsTable(string id)
    {
        if (!ImGui.BeginTable(id, 2, ImGuiTableFlags.SizingStretchProp))
            return false;

        ImGui.TableSetupColumn("##SettingLabel", ImGuiTableColumnFlags.WidthFixed, 128f);
        ImGui.TableSetupColumn("##SettingControl", ImGuiTableColumnFlags.WidthStretch);
        return true;
    }

    internal static void DrawSettingLabel(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
    }

    internal sealed class PanelScope : IDisposable
    {
        public PanelScope(string id, Vector2 size, Vector4 borderColor)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelBackground);
            ImGui.PushStyleColor(ImGuiCol.Border, borderColor);
            ImGui.PushStyleColor(ImGuiCol.Separator, AccentSoft);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 12f));
            ImGui.BeginChild(id, size, true);
        }

        public void Dispose()
        {
            ImGui.EndChild();
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(3);
        }
    }
}
