using Dalamud.Plugin;

namespace StarLoom.Config;

public sealed class ConfigStore
{
    private readonly IDalamudPluginInterface pluginInterface;

    internal bool defaultsInjectedOnLoad { get; }
    public PluginConfig pluginConfig { get; }

    public ConfigStore(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        pluginConfig = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        defaultsInjectedOnLoad = ConfigDefaults.HasMissingValues(pluginConfig);
        ConfigDefaults.Apply(pluginConfig);

        if (defaultsInjectedOnLoad)
            Save();
    }

    public void Save()
    {
        pluginInterface.SavePluginConfig(pluginConfig);
    }
}
