using ECommons.DalamudServices;

namespace Starloom.Services;

public sealed class ConfigurationStore
{
    public Configuration Configuration { get; }

    public ConfigurationStore()
    {
        Configuration = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new();
    }

    public bool EnsureDefaults()
    {
        var updated = Configuration.EnsurePostPurchaseDefaults();
        if (updated)
            Save();

        return updated;
    }

    public void Save()
        => Svc.PluginInterface.SavePluginConfig(Configuration);
}
