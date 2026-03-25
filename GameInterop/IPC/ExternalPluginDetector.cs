using ECommons.DalamudServices;
using ECommons.Reflection;
using System;

namespace Starloom.GameInterop.IPC;

internal static class ExternalPluginDetector
{
    public static bool IsAvailable(string internalName)
    {
        if (string.IsNullOrWhiteSpace(internalName))
            return false;

        try
        {
            return DalamudReflector.TryGetDalamudPlugin(internalName, out _, true, false);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
