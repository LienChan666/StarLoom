using Dalamud.Plugin;
using StarLoom.Config;
using StarLoom.Tasks;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace StarLoom.Tests.Config;

public sealed class ConfigStoreTests
{
    [Fact]
    public void Constructor_Should_Save_Plugin_Config_When_Defaults_Are_Injected()
    {
        var proxy = PluginInterfaceProxy.Create(new PluginConfig
        {
            uiLanguage = string.Empty,
            postPurchaseAction = (PostPurchaseAction)999,
            freeSlotThreshold = 0,
            defaultReturnPoint = null,
        });

        _ = new ConfigStore(proxy.PluginInterface);

        Assert.Equal(1, proxy.SaveCalls);
        Assert.IsType<PluginConfig>(proxy.LastSavedConfig);
        Assert.NotNull(proxy.SavedPluginConfig.defaultReturnPoint);
        Assert.Equal("zh", proxy.SavedPluginConfig.uiLanguage);
        Assert.Equal(PostPurchaseAction.ReturnToConfiguredPoint, proxy.SavedPluginConfig.postPurchaseAction);
        Assert.Equal(10, proxy.SavedPluginConfig.freeSlotThreshold);
    }

    [Fact]
    public void CollectableShopConfig_Should_Expose_Runtime_Travel_Metadata()
    {
        AssertProperty("location", typeof(Vector3));
        AssertProperty("scripShopNpcId", typeof(uint));
        AssertProperty("scripShopLocation", typeof(Vector3));
        AssertProperty("isLifestreamRequired", typeof(bool));
        AssertProperty("lifestreamCommand", typeof(string));
    }

    private static void AssertProperty(string propertyName, Type propertyType)
    {
        var property = typeof(CollectableShopConfig).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(propertyType, property!.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
    }

    private class PluginInterfaceProxy : DispatchProxy
    {
        public object? PluginConfig { get; private set; }
        public object? LastSavedConfig { get; private set; }
        public int SaveCalls { get; private set; }

        public PluginConfig SavedPluginConfig => Assert.IsType<PluginConfig>(LastSavedConfig);

        public IDalamudPluginInterface PluginInterface => (IDalamudPluginInterface)(object)this;

        public static PluginInterfaceProxy Create(object? pluginConfig)
        {
            var pluginInterface = Create<IDalamudPluginInterface, PluginInterfaceProxy>();
            var proxy = (PluginInterfaceProxy)(object)pluginInterface;
            proxy.PluginConfig = pluginConfig;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "GetPluginConfig" => PluginConfig,
                "SavePluginConfig" => Save(args),
                _ => GetDefaultReturnValue(targetMethod),
            };
        }

        private object? Save(object?[]? args)
        {
            SaveCalls++;
            LastSavedConfig = args?[0];
            PluginConfig = LastSavedConfig;
            return null;
        }

        private static object? GetDefaultReturnValue(MethodInfo? targetMethod)
        {
            if (targetMethod == null || targetMethod.ReturnType == typeof(void))
                return null;

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}

public sealed class ConfigStoreTests_StarLoomLifecycle
{
    [Fact]
    public void Dispose_Should_Stop_Active_Workflow_Before_Teardown()
    {
        var workflowTask = WorkflowTask.CreateForTests();
        workflowTask.StartTurnInOnly();

        Assert.True(workflowTask.isBusy);

        var stopMethod = typeof(StarLoom).GetMethod("StopWorkflowBeforeDispose", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(stopMethod);

        stopMethod!.Invoke(null, [workflowTask]);

        Assert.False(workflowTask.isBusy);
        Assert.Equal("Idle", workflowTask.currentStage);
    }
}
