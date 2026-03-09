using ECommons.DalamudServices;
using System;

namespace StarLoom.IPC;

public sealed class IpcCallRunner
{
    private readonly string _serviceName;
    private readonly string _pluginName;

    public IpcCallRunner(string serviceName, string pluginName)
    {
        _serviceName = serviceName;
        _pluginName = pluginName;
    }

    public bool IsAvailable()
        => ExternalPluginDetector.IsAvailable(_pluginName);

    public void EnsureAvailable()
    {
        if (!IsAvailable())
            throw new InvalidOperationException($"{_pluginName} IPC is unavailable.");
    }

    public TResult InvokeFunc<TResult>(string name, TResult failureResult = default!, bool requireAvailable = false, int retries = 0)
        => InvokeCore(
            name,
            () => Svc.PluginInterface.GetIpcSubscriber<TResult>(name).InvokeFunc(),
            failureResult,
            requireAvailable,
            retries);

    public TResult InvokeFunc<T1, T2, TResult>(string name, T1 arg1, T2 arg2, TResult failureResult = default!, bool requireAvailable = false, int retries = 0)
        => InvokeCore(
            name,
            () => Svc.PluginInterface.GetIpcSubscriber<T1, T2, TResult>(name).InvokeFunc(arg1, arg2),
            failureResult,
            requireAvailable,
            retries);

    public bool InvokeAction(string name, bool requireAvailable = false, int retries = 0)
        => InvokeCore(
            name,
            () =>
            {
                Svc.PluginInterface.GetIpcSubscriber<object>(name).InvokeAction();
                return true;
            },
            false,
            requireAvailable,
            retries);

    public bool InvokeAction<T>(string name, T arg, bool requireAvailable = false, int retries = 0)
        => InvokeCore(
            name,
            () =>
            {
                Svc.PluginInterface.GetIpcSubscriber<T, object>(name).InvokeAction(arg);
                return true;
            },
            false,
            requireAvailable,
            retries);

    private TResult InvokeCore<TResult>(string name, Func<TResult> action, TResult failureResult, bool requireAvailable, int retries)
    {
        if (!IsAvailable())
        {
            if (requireAvailable)
                throw new InvalidOperationException($"{_pluginName} IPC is unavailable.");

            return failureResult;
        }

        Exception? lastError = null;
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (requireAvailable && lastError != null)
            throw new InvalidOperationException($"{_pluginName} IPC call failed: {name}", lastError);

        return failureResult;
    }
}
