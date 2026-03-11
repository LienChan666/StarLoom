using ECommons.Reflection;

namespace StarLoom.Ipc;

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
        catch (Exception exception)
        {
            Svc.Log.Debug(exception, "Failed to probe plugin availability for {PluginName}", internalName);
            return false;
        }
    }
}
