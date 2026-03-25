using System;

namespace Starloom.UI.Components.Shared;

internal static class LayoutMetrics
{
    internal static HomeLayoutMetrics CreateHome(float width, float height, float spacing)
    {
        var leftWidth = Math.Clamp(width * 0.30f, 280f, 340f);
        var rightWidth = Math.Max(0f, width - leftWidth - spacing);
        var topHeight = Math.Max(190f, (height - spacing) * 0.44f);
        var bottomHeight = Math.Max(0f, height - topHeight - spacing);
        return new HomeLayoutMetrics(leftWidth, rightWidth, topHeight, bottomHeight);
    }
}

internal readonly record struct HomeLayoutMetrics(float LeftWidth, float RightWidth, float TopHeight, float BottomHeight);
