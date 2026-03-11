using StarLoom.Ipc;

namespace StarLoom.Tasks.Navigation;

public sealed class NavigationTask
{
    private readonly IVNavmeshIpc vNavmeshIpc;
    private readonly ILifestreamIpc lifestreamIpc;

    private NavigationRequest currentRequest;
    private bool hasActiveRequest;
    private bool isWaitingForLifestream;

    public bool isRunning { get; private set; }
    public bool isCompleted { get; private set; }
    public bool hasFailed { get; private set; }
    public string? errorMessage { get; private set; }

    public NavigationTask() : this(new VNavmeshIpc(), new LifestreamIpc())
    {
    }

    public NavigationTask(IVNavmeshIpc vNavmeshIpc, ILifestreamIpc lifestreamIpc)
    {
        this.vNavmeshIpc = vNavmeshIpc;
        this.lifestreamIpc = lifestreamIpc;
    }

    public void Start(NavigationRequest navigationRequest)
    {
        currentRequest = navigationRequest;
        hasActiveRequest = true;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
        isWaitingForLifestream = false;
        isRunning = true;

        if (NavigationPlan.ShouldUseLifestream(navigationRequest))
        {
            if (!lifestreamIpc.IsAvailable())
            {
                Fail("Lifestream IPC is unavailable.");
                return;
            }

            lifestreamIpc.ExecuteCommand(navigationRequest.lifestreamCommand);
            isWaitingForLifestream = true;
            TryLogInformation("Navigation started via Lifestream for {Reason}", navigationRequest.reason);
            return;
        }

        StartPath();
    }

    public void Update()
    {
        if (!isRunning || isCompleted || hasFailed || !hasActiveRequest)
            return;

        if (isWaitingForLifestream)
        {
            if (lifestreamIpc.IsBusy())
                return;

            isWaitingForLifestream = false;
            StartPath();
            return;
        }

        if (!vNavmeshIpc.IsAvailable())
        {
            Fail("VNavmesh IPC is unavailable.");
            return;
        }

        if (vNavmeshIpc.IsPathRunning())
            return;

        isCompleted = true;
        isRunning = false;
        TryLogInformation("Navigation completed for {Reason}", currentRequest.reason);
    }

    public void Stop()
    {
        if (isWaitingForLifestream && lifestreamIpc.IsAvailable() && lifestreamIpc.IsBusy())
            lifestreamIpc.Abort();

        if (vNavmeshIpc.IsAvailable())
            vNavmeshIpc.Stop();

        hasActiveRequest = false;
        isWaitingForLifestream = false;
        isRunning = false;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
    }

    private void StartPath()
    {
        if (!vNavmeshIpc.IsAvailable())
        {
            Fail("VNavmesh IPC is unavailable.");
            return;
        }

        if (!vNavmeshIpc.PathfindAndMoveTo(currentRequest.destination, currentRequest.allowFlight))
        {
            Fail($"Failed to start navigation for {currentRequest.reason}.");
            return;
        }

        TryLogInformation("Navigation path requested for {Reason}", currentRequest.reason);
    }

    private void Fail(string message)
    {
        errorMessage = message;
        hasFailed = true;
        isRunning = false;
        isCompleted = false;
        hasActiveRequest = false;
        isWaitingForLifestream = false;
        TryLogError("Navigation failed: {Message}", message);
        TryDuoLogError(message);
    }

    private static void TryLogInformation(string messageTemplate, params object[] arguments)
    {
        if (Svc.Log == null)
            return;

        Svc.Log.Information(messageTemplate, arguments);
    }

    private static void TryLogError(string messageTemplate, params object[] arguments)
    {
        if (Svc.Log == null)
            return;

        Svc.Log.Error(messageTemplate, arguments);
    }

    private static void TryDuoLogError(string message)
    {
        try
        {
            DuoLog.Error(message);
        }
        catch
        {
        }
    }
}
