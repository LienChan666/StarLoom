using Dalamud.Plugin;

namespace StarLoom.Config;

public sealed class ConfigStore
{
    private readonly IDalamudPluginInterface pluginInterface;

    public PluginConfig pluginConfig { get; }

    public ConfigStore(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        pluginConfig = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        ConfigDefaults.Apply(pluginConfig);
    }

    public void Save()
    {
        pluginInterface.SavePluginConfig(pluginConfig);
    }
}
