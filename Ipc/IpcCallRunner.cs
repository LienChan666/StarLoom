namespace StarLoom.Ipc;

public sealed class IpcCallRunner
{
    private readonly string serviceName;
    private readonly string pluginName;

    public IpcCallRunner(string serviceName, string pluginName)
    {
        this.serviceName = serviceName;
        this.pluginName = pluginName;
    }

    public bool IsAvailable()
    {
        return ExternalPluginDetector.IsAvailable(pluginName);
    }

    public TResult InvokeFunc<TResult>(string name, TResult failureResult = default!, bool requireAvailable = false)
    {
        return InvokeCore(
            name,
            () => Svc.PluginInterface.GetIpcSubscriber<TResult>(name).InvokeFunc(),
            failureResult,
            requireAvailable);
    }

    public TResult InvokeFunc<T1, T2, TResult>(string name, T1 argument1, T2 argument2, TResult failureResult = default!, bool requireAvailable = false)
    {
        return InvokeCore(
            name,
            () => Svc.PluginInterface.GetIpcSubscriber<T1, T2, TResult>(name).InvokeFunc(argument1, argument2),
            failureResult,
            requireAvailable);
    }

    public bool InvokeAction(string name, bool requireAvailable = false)
    {
        return InvokeCore(
            name,
            () =>
            {
                Svc.PluginInterface.GetIpcSubscriber<object>(name).InvokeAction();
                return true;
            },
            false,
            requireAvailable);
    }

    public bool InvokeAction<T>(string name, T argument, bool requireAvailable = false)
    {
        return InvokeCore(
            name,
            () =>
            {
                Svc.PluginInterface.GetIpcSubscriber<T, object>(name).InvokeAction(argument);
                return true;
            },
            false,
            requireAvailable);
    }

    public void EnsureAvailable()
    {
        if (!IsAvailable())
            throw new InvalidOperationException($"{pluginName} IPC is unavailable.");
    }

    private TResult InvokeCore<TResult>(string name, Func<TResult> action, TResult failureResult, bool requireAvailable)
    {
        if (!IsAvailable())
        {
            if (requireAvailable)
                throw new InvalidOperationException($"{pluginName} IPC is unavailable.");

            return failureResult;
        }

        try
        {
            return action();
        }
        catch (Exception exception)
        {
            Svc.Log.Warning(exception, "{ServiceName} IPC call failed: {CallName}", serviceName, name);

            if (requireAvailable)
                throw new InvalidOperationException($"{pluginName} IPC call failed: {name}", exception);

            return failureResult;
        }
    }
}
