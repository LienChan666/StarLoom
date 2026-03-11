using StarLoom.Game;
using StarLoom.Ipc;

namespace StarLoom.Tasks.Navigation;

public sealed class NavigationTask
{
    private static readonly TimeSpan LocalActionReadyStableDuration = TimeSpan.FromMilliseconds(500);

    private readonly IVNavmeshIpc vNavmeshIpc;
    private readonly ILifestreamIpc lifestreamIpc;
    private readonly Func<NavigationRuntimeSnapshot> getSnapshot;
    private readonly Func<uint, byte, bool> teleportToAetheryte;
    private readonly Func<DateTime> getUtcNow;

    private NavigationRequest currentRequest;
    private NavigationStage currentNavigationStage = NavigationStage.Idle;
    private bool hasActiveRequest;
    private bool lifestreamAttempted;
    private DateTime lastActionAt = DateTime.MinValue;
    private DateTime movementStartedAt = DateTime.MinValue;
    private DateTime? localControlReadyAt;

    public string currentStage => currentNavigationStage.ToString();
    public bool isRunning { get; private set; }
    public bool isCompleted { get; private set; }
    public bool hasFailed { get; private set; }
    public string? errorMessage { get; private set; }

    public NavigationTask() : this(new VNavmeshIpc(), new LifestreamIpc())
    {
    }

    public NavigationTask(
        IVNavmeshIpc vNavmeshIpc,
        ILifestreamIpc lifestreamIpc,
        Func<NavigationRuntimeSnapshot>? getSnapshot = null,
        Func<uint, byte, bool>? teleportToAetheryte = null,
        Func<DateTime>? getUtcNow = null)
    {
        this.vNavmeshIpc = vNavmeshIpc;
        this.lifestreamIpc = lifestreamIpc;
        this.getSnapshot = getSnapshot ?? new LocationGame().GetNavigationSnapshot;
        this.teleportToAetheryte = teleportToAetheryte ?? LocationGame.TeleportToAetheryte;
        this.getUtcNow = getUtcNow ?? (() => DateTime.UtcNow);
    }

    public void Start(NavigationRequest navigationRequest)
    {
        currentRequest = navigationRequest;
        hasActiveRequest = true;
        lifestreamAttempted = false;
        lastActionAt = DateTime.MinValue;
        movementStartedAt = DateTime.MinValue;
        localControlReadyAt = null;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
        isRunning = true;
        currentNavigationStage = NavigationStage.Teleporting;
        TryLogInformation("Navigation started for {Reason}", navigationRequest.reason);
    }

    public void Update()
    {
        if (!hasActiveRequest || !isRunning || isCompleted || hasFailed)
            return;

        var snapshot = getSnapshot();
        if (!HasStableLocalControl(snapshot))
            return;

        if (NavigationPlan.HasArrived(currentRequest, snapshot))
        {
            Complete();
            return;
        }

        switch (currentNavigationStage)
        {
            case NavigationStage.Teleporting:
                HandleTeleport(snapshot);
                return;
            case NavigationStage.WaitingForTeleport:
                HandleWaitingForTeleport(snapshot);
                return;
            case NavigationStage.UsingLifestream:
                HandleLifestream(snapshot);
                return;
            case NavigationStage.WaitingForLifestream:
                HandleWaitingForLifestream();
                return;
            case NavigationStage.Pathfinding:
                HandlePathfinding();
                return;
            case NavigationStage.WaitingForArrival:
                HandleWaitForArrival(snapshot);
                return;
        }
    }

    public void Stop()
    {
        if (lifestreamIpc.IsAvailable() && lifestreamIpc.IsBusy())
            lifestreamIpc.Abort();

        if (vNavmeshIpc.IsAvailable())
            vNavmeshIpc.Stop();

        Reset();
    }

    private void HandleTeleport(NavigationRuntimeSnapshot snapshot)
    {
        if (snapshot.currentTerritoryId == currentRequest.territoryId)
        {
            var distance = NavigationPlan.GetDistanceToDestination(currentRequest, snapshot);
            currentNavigationStage = NavigationPlan.ShouldUseLifestream(currentRequest, distance) && !lifestreamAttempted
                ? NavigationStage.UsingLifestream
                : NavigationStage.Pathfinding;
            return;
        }

        if (currentRequest.aetheryteId == 0)
        {
            Fail("Teleport target is missing.");
            return;
        }

        if (!teleportToAetheryte(currentRequest.aetheryteId, 0))
        {
            Fail("Teleport failed.");
            return;
        }

        lastActionAt = getUtcNow();
        currentNavigationStage = NavigationStage.WaitingForTeleport;
    }

    private void HandleWaitingForTeleport(NavigationRuntimeSnapshot snapshot)
    {
        if (snapshot.currentTerritoryId == currentRequest.territoryId)
        {
            var distance = NavigationPlan.GetDistanceToDestination(currentRequest, snapshot);
            currentNavigationStage = NavigationPlan.ShouldUseLifestream(currentRequest, distance) && !lifestreamAttempted
                ? NavigationStage.UsingLifestream
                : NavigationStage.Pathfinding;
            return;
        }

        if ((getUtcNow() - lastActionAt) > TimeSpan.FromSeconds(5))
            currentNavigationStage = NavigationStage.UsingLifestream;
    }

    private void HandleLifestream(NavigationRuntimeSnapshot snapshot)
    {
        var distance = NavigationPlan.GetDistanceToDestination(currentRequest, snapshot);
        if (!NavigationPlan.ShouldUseLifestream(currentRequest, distance))
        {
            currentNavigationStage = NavigationStage.Pathfinding;
            return;
        }

        if (!lifestreamIpc.IsAvailable())
        {
            Fail("Lifestream is required.");
            return;
        }

        lifestreamIpc.ExecuteCommand(currentRequest.lifestreamCommand);
        lifestreamAttempted = true;
        lastActionAt = getUtcNow();
        currentNavigationStage = NavigationStage.WaitingForLifestream;
    }

    private void HandleWaitingForLifestream()
    {
        if (!lifestreamIpc.IsBusy())
            currentNavigationStage = NavigationStage.Pathfinding;
    }

    private void HandlePathfinding()
    {
        if (!vNavmeshIpc.IsAvailable())
        {
            Fail("VNavmesh IPC is unavailable.");
            return;
        }

        if (!vNavmeshIpc.IsPathRunning())
        {
            if (!vNavmeshIpc.PathfindAndMoveTo(currentRequest.destination, currentRequest.allowFlight))
            {
                Fail("Pathfinding failed.");
                return;
            }

            movementStartedAt = getUtcNow();
        }

        lastActionAt = getUtcNow();
        currentNavigationStage = NavigationStage.WaitingForArrival;
    }

    private void HandleWaitForArrival(NavigationRuntimeSnapshot snapshot)
    {
        if (snapshot.currentTerritoryId != currentRequest.territoryId)
        {
            currentNavigationStage = NavigationStage.Teleporting;
            return;
        }

        var distance = NavigationPlan.GetDistanceToDestination(currentRequest, snapshot);
        if (distance <= currentRequest.arrivalDistance)
        {
            Complete();
            return;
        }

        if ((getUtcNow() - movementStartedAt) > TimeSpan.FromSeconds(60))
        {
            Fail($"Navigation timed out. Distance to target: {distance:F1}m.");
            return;
        }

        if (!vNavmeshIpc.IsPathRunning() && (getUtcNow() - lastActionAt) > TimeSpan.FromMilliseconds(500))
        {
            if (!vNavmeshIpc.PathfindAndMoveTo(currentRequest.destination, currentRequest.allowFlight))
            {
                Fail("Pathfinding failed.");
                return;
            }

            lastActionAt = getUtcNow();
        }
    }

    private bool HasStableLocalControl(NavigationRuntimeSnapshot snapshot)
    {
        if (!snapshot.isLocalControlReady)
        {
            localControlReadyAt = null;
            return false;
        }

        if (localControlReadyAt is null)
        {
            localControlReadyAt = getUtcNow();
            return false;
        }

        return (getUtcNow() - localControlReadyAt.Value) >= LocalActionReadyStableDuration;
    }

    private void Complete()
    {
        if (vNavmeshIpc.IsAvailable())
            vNavmeshIpc.Stop();

        isCompleted = true;
        isRunning = false;
        hasActiveRequest = false;
        currentNavigationStage = NavigationStage.Arrived;
        TryLogInformation("Navigation completed for {Reason}", currentRequest.reason);
    }

    private void Fail(string message)
    {
        if (vNavmeshIpc.IsAvailable())
            vNavmeshIpc.Stop();

        errorMessage = message;
        hasFailed = true;
        isRunning = false;
        isCompleted = false;
        hasActiveRequest = false;
        currentNavigationStage = NavigationStage.Failed;
        TryLogError("Navigation failed: {Message}", message);
        TryDuoLogError(message);
    }

    private void Reset()
    {
        currentRequest = default;
        currentNavigationStage = NavigationStage.Idle;
        hasActiveRequest = false;
        lifestreamAttempted = false;
        lastActionAt = DateTime.MinValue;
        movementStartedAt = DateTime.MinValue;
        localControlReadyAt = null;
        isRunning = false;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
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
