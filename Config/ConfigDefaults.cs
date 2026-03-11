namespace StarLoom.Config;

public static class ConfigDefaults
{
    internal static bool HasMissingValues(PluginConfig pluginConfig)
    {
        return pluginConfig.defaultReturnPoint == null
            || string.IsNullOrWhiteSpace(pluginConfig.uiLanguage)
            || !Enum.IsDefined(pluginConfig.postPurchaseAction)
            || pluginConfig.freeSlotThreshold <= 0;
    }

    public static void Apply(PluginConfig pluginConfig)
    {
        if (pluginConfig.defaultReturnPoint == null)
            pluginConfig.defaultReturnPoint = ReturnPointConfig.CreateInn();

        if (string.IsNullOrWhiteSpace(pluginConfig.uiLanguage))
            pluginConfig.uiLanguage = "zh";

        if (!Enum.IsDefined(pluginConfig.postPurchaseAction))
            pluginConfig.postPurchaseAction = PostPurchaseAction.ReturnToConfiguredPoint;

        if (pluginConfig.freeSlotThreshold <= 0)
            pluginConfig.freeSlotThreshold = 10;
    }
}
