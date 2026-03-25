using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Shared;

internal sealed class ChildScope : IDisposable
{
    public ChildScope(string id, Vector2 size)
    {
        ImGui.BeginChild(id, size, true);
    }

    public void Dispose()
        => ImGui.EndChild();
}
