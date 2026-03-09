using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Shared;

internal static class GamePanelStyle
{
    internal static readonly Vector4 Layer0 = new(0.067f, 0.074f, 0.106f, 0.98f);
    internal static readonly Vector4 Layer1 = new(0.090f, 0.102f, 0.145f, 0.98f);
    internal static readonly Vector4 Layer2 = new(0.118f, 0.137f, 0.192f, 0.96f);

    internal static readonly Vector4 PanelBackground = Layer1;
    internal static readonly Vector4 PanelBackgroundAlt = Layer2;

    internal static readonly Vector4 Accent = new(0.345f, 0.722f, 0.831f, 1f);
    internal static readonly Vector4 AccentSoft = new(0.220f, 0.478f, 0.569f, 1f);
    internal static readonly Vector4 Gold = new(0.878f, 0.741f, 0.420f, 1f);
    internal static readonly Vector4 GoldSoft = new(0.576f, 0.486f, 0.275f, 1f);

    internal static readonly Vector4 Success = new(0.400f, 0.800f, 0.553f, 1f);
    internal static readonly Vector4 Warning = new(0.922f, 0.722f, 0.333f, 1f);
    internal static readonly Vector4 Danger = new(0.882f, 0.392f, 0.373f, 1f);

    internal static readonly Vector4 TextPrimary = new(0.906f, 0.929f, 0.969f, 1f);
    internal static readonly Vector4 TextSecond = new(0.620f, 0.675f, 0.765f, 1f);
    internal static readonly Vector4 TextMuted = new(0.420f, 0.467f, 0.553f, 1f);
    internal static readonly Vector4 MutedText = TextSecond;

    internal static readonly Vector4 PanelBorder = new(0.180f, 0.212f, 0.290f, 0.70f);
    internal static readonly Vector4 BorderSubtle = new(0.180f, 0.212f, 0.290f, 0.70f);
    internal static readonly Vector4 BorderAccent = new(0.345f, 0.722f, 0.831f, 0.50f);
    internal static readonly Vector4 SeparatorDim = new(0.200f, 0.235f, 0.310f, 0.50f);

    internal static class Spacing
    {
        internal const float Xs = 4f;
        internal const float Sm = 8f;
        internal const float Md = 12f;
        internal const float Lg = 16f;
        internal const float Xl = 24f;
        internal const float Xxl = 32f;
    }

    internal static PanelScope BeginPanel(string id, Vector2 size, Vector4? borderColor = null, Vector4? accentBarColor = null)
        => new(id, size, borderColor ?? PanelBorder, accentBarColor);

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

    /// <summary>在文字前绘制一个 6px 直径的状态圆点。</summary>
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

    /// <summary>绘制药丸形 badge。</summary>
    internal static void DrawPillBadge(string text, Vector4 accentColor)
    {
        var drawList = ImGui.GetWindowDrawList();
        var textSize = ImGui.CalcTextSize(text);
        var padX = 10f;
        var padY = 4f;
        var cursorPos = ImGui.GetCursorScreenPos();

        var min = cursorPos;
        var max = new Vector2(cursorPos.X + textSize.X + padX * 2f, cursorPos.Y + textSize.Y + padY * 2f);

        var bgColor = Tint(accentColor, 0.25f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(bgColor), 10f);

        var textColor = Tint(accentColor, 1.5f);
        drawList.AddText(new Vector2(cursorPos.X + padX, cursorPos.Y + padY), ImGui.GetColorU32(textColor), text);

        ImGui.Dummy(new Vector2(textSize.X + padX * 2f, textSize.Y + padY * 2f));
    }

    internal static void DrawPanelHeader(string title, string description)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Accent);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        if (!string.IsNullOrWhiteSpace(description))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, TextSecond);
            ImGui.TextWrapped(description);
            ImGui.PopStyleColor();
        }

        DrawGradientSeparator();
    }

    internal static void DrawSectionTitle(string title, string description)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Accent);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        if (!string.IsNullOrWhiteSpace(description))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, TextMuted);
            ImGui.TextWrapped(description);
            ImGui.PopStyleColor();
        }
    }

    internal static void DrawInfoRow(string label, string value)
    {
        if (ImGui.BeginTable($"##InfoRow_{label}", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("##Label", ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableSetupColumn("##Value", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.PushStyleColor(ImGuiCol.Text, TextSecond);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(1);
            ImGui.PushStyleColor(ImGuiCol.Text, TextPrimary);
            ImGui.TextWrapped(value);
            ImGui.PopStyleColor();

            ImGui.EndTable();
        }
    }

    internal static void DrawHint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, TextMuted);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    internal static void DrawInlineBadge(string label, string value, Vector4 accent)
    {
        DrawPillBadge($"{label} · {value}", accent);
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
        ImGui.PushStyleColor(ImGuiCol.Text, TextSecond);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
    }

    /// <summary>绘制带可选图标前缀的操作按钮。</summary>
    internal static bool DrawActionButton(string label, Vector4 color, float width, bool enabled, string? icon = null)
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
        var displayLabel = icon != null ? $"{icon} {label}" : label;
        var clicked = ImGui.Button(displayLabel, new Vector2(width, 34f));
        ImGui.EndDisabled();

        if (!enabled)
            ImGui.PopStyleColor(4);
        else
            ImGui.PopStyleColor(3);
        ImGui.PopStyleVar();

        return clicked;
    }

    /// <summary>为表格推入统一的表格配色样式。调用后需配对 PopTableStyle()。</summary>
    internal static void PushTableStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, Layer0);
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(Layer2.X, Layer2.Y, Layer2.Z, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight, SeparatorDim);
    }

    /// <summary>弹出 PushTableStyle 推入的样式。</summary>
    internal static void PopTableStyle()
    {
        ImGui.PopStyleColor(4);
    }

    internal sealed class PanelScope : IDisposable
    {
        private readonly Vector4? _accentBarColor;

        public PanelScope(string id, Vector2 size, Vector4 borderColor, Vector4? accentBarColor = null)
        {
            _accentBarColor = accentBarColor;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Layer1);
            ImGui.PushStyleColor(ImGuiCol.Border, borderColor);
            ImGui.PushStyleColor(ImGuiCol.Separator, SeparatorDim);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Spacing.Lg, 14f));
            ImGui.BeginChild(id, size, true);
        }

        public void Dispose()
        {
            if (_accentBarColor.HasValue)
            {
                var drawList = ImGui.GetWindowDrawList();
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                drawList.AddRectFilled(
                    windowPos,
                    new Vector2(windowPos.X + 3f, windowPos.Y + windowSize.Y),
                    ImGui.GetColorU32(_accentBarColor.Value),
                    8f,
                    ImDrawFlags.RoundCornersLeft);
            }

            ImGui.EndChild();
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(3);
        }
    }
}
