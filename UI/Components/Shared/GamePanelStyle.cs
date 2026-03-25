using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Starloom.UI.Components.Shared;

internal static class GamePanelStyle
{
    internal static readonly Vector4 Layer0 = new(0.067f, 0.074f, 0.106f, 0.98f);
    internal static readonly Vector4 Layer1 = new(0.090f, 0.102f, 0.145f, 0.98f);
    internal static readonly Vector4 Layer2 = new(0.118f, 0.137f, 0.192f, 0.96f);

    internal static readonly Vector4 Accent = new(0.345f, 0.722f, 0.831f, 1f);
    internal static readonly Vector4 Gold = new(0.878f, 0.741f, 0.420f, 1f);

    internal static readonly Vector4 Success = new(0.400f, 0.800f, 0.553f, 1f);
    internal static readonly Vector4 Danger = new(0.882f, 0.392f, 0.373f, 1f);

    internal static readonly Vector4 TextPrimary = new(0.906f, 0.929f, 0.969f, 1f);
    internal static readonly Vector4 TextSecond = new(0.620f, 0.675f, 0.765f, 1f);
    internal static readonly Vector4 TextMuted = new(0.420f, 0.467f, 0.553f, 1f);

    internal static readonly Vector4 BorderAccent = new(0.345f, 0.722f, 0.831f, 0.50f);
    internal static readonly Vector4 SeparatorDim = new(0.200f, 0.235f, 0.310f, 0.50f);

    internal static class Spacing
    {
        internal const float Sm = 8f;
        internal const float Md = 12f;
        internal const float Lg = 16f;
    }

    internal static Vector4 Tint(Vector4 color, float factor)
        => new(
            Math.Clamp(color.X * factor, 0f, 1f),
            Math.Clamp(color.Y * factor, 0f, 1f),
            Math.Clamp(color.Z * factor, 0f, 1f),
            color.W);

    internal static void DrawGradientSeparator()
    {
        var drawList = ImGui.GetWindowDrawList();
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var y = cursorScreenPos.Y + Spacing.Sm / 2f;

        var leftColor = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.6f);
        var rightColor = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.0f);

        drawList.AddRectFilledMultiColor(
            new Vector2(cursorScreenPos.X, y),
            new Vector2(cursorScreenPos.X + availWidth, y + 1f),
            ImGui.GetColorU32(leftColor),
            ImGui.GetColorU32(rightColor),
            ImGui.GetColorU32(rightColor),
            ImGui.GetColorU32(leftColor));

        ImGui.Dummy(new Vector2(0, Spacing.Sm));
    }

    /// <summary>Draw a 6px status dot before the current text line.</summary>
    internal static void DrawStatusDot(Vector4 color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var center = new Vector2(cursorPos.X + 5f, cursorPos.Y + lineHeight / 2f);
        drawList.AddCircleFilled(center, 3f, ImGui.GetColorU32(color), 12);
        ImGui.Dummy(new Vector2(14f, lineHeight));
        ImGui.SameLine();
    }

    internal static bool BeginSettingsTable(string id)
    {
        if (!ImGui.BeginTable(id, 2, ImGuiTableFlags.SizingStretchProp))
            return false;

        ImGui.TableSetupColumn("##SettingLabel", ImGuiTableColumnFlags.WidthFixed, 128f);
        ImGui.TableSetupColumn("##SettingControl", ImGuiTableColumnFlags.WidthStretch);
        return true;
    }

    /// <summary>Draw an action button with an optional icon and stable ImGui id.</summary>
    internal static bool DrawActionButton(string id, string label, Vector4 color, float width, bool enabled, string? icon = null)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        if (enabled)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Tint(color, 0.40f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Tint(color, 0.60f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Tint(color, 0.75f));
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Layer2);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Layer2);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Layer2);
            ImGui.PushStyleColor(ImGuiCol.Text, TextMuted);
        }

        ImGui.BeginDisabled(!enabled);
        var displayLabel = icon != null ? $"{icon} {label}##{id}" : $"{label}##{id}";
        var clicked = ImGui.Button(displayLabel, new Vector2(width, 34f));
        ImGui.EndDisabled();

        if (!enabled)
            ImGui.PopStyleColor(4);
        else
            ImGui.PopStyleColor(3);
        ImGui.PopStyleVar();

        return clicked;
    }

    /// <summary>Push shared table styling. Pair this with PopTableStyle().</summary>
    internal static void PushTableStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, Layer0);
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(Layer2.X, Layer2.Y, Layer2.Z, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight, SeparatorDim);
    }

    /// <summary>Pop the colors pushed by PushTableStyle().</summary>
    internal static void PopTableStyle()
    {
        ImGui.PopStyleColor(4);
    }
}
