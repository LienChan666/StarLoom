using StarLoom.Config;
using StarLoom.Game;
using StarLoom.Ipc;
using StarLoom.Tasks.Navigation;
using System.Numerics;

namespace StarLoom.Tasks.Return;

public interface IReturnTaskRuntime
{
    bool TryResolveConfiguredPoint(ReturnPointConfig configuredPoint, out ReturnPointConfig resolvedPoint);
    bool CanEnterDirectlyFromCurrentLocation(ReturnPointConfig point);
    bool TryTeleportToReturnPoint(ReturnPointConfig point, ILifestreamIpc lifestreamIpc);
    bool IsTransitioning();
    bool IsInsideInn();
    bool IsInsideHouse();
    uint GetCurrentTerritoryId();
    bool TryGetHousingEntrance(bool isApartment, out Vector3 entrancePosition);
    bool TryInteractHousingEntrance(bool isApartment);
    bool TryConfirmEntry(bool isApartment);
}

public sealed class ReturnTask
{
    private enum ReturnStage
    {
        Idle,
        WaitingForTeleport,
        WaitingForInn,
        MovingToEntrance,
        InteractingEntrance,
        ConfirmingEntry,
        WaitingForIndoor,
        Completed,
        Failed,
    }

    private static readonly TimeSpan InnTeleportTimeout = TimeSpan.FromMinutes(5);

    private readonly NavigationTask navigationTask;
    private readonly ILifestreamIpc lifestreamIpc;
    private readonly IReturnTaskRuntime runtime;
    private readonly Func<DateTime> getUtcNow;

    private ReturnPointConfig? targetPoint;
    private ReturnStage stage = ReturnStage.Idle;
    private DateTime lastActionAt = DateTime.MinValue;
    private DateTime transitionedAt = DateTime.MinValue;
    private bool observedTransition;
    private bool navigationStarted;

    public string currentStage => stage.ToString();
    public bool isRunning { get; private set; }
    public bool isCompleted { get; private set; }
    public bool hasFailed { get; private set; }
    public string? errorMessage { get; private set; }

    public ReturnTask() : this(new NavigationTask(), new LifestreamIpc())
    {
    }

    public ReturnTask(
        NavigationTask navigationTask,
        ILifestreamIpc lifestreamIpc,
        IReturnTaskRuntime? runtime = null,
        Func<DateTime>? getUtcNow = null)
    {
        this.navigationTask = navigationTask;
        this.lifestreamIpc = lifestreamIpc;
        this.runtime = runtime ?? new LiveReturnTaskRuntime(new LocationGame(), new NpcGame());
        this.getUtcNow = getUtcNow ?? (() => DateTime.UtcNow);
    }

    public void Start(ReturnPointConfig configuredPoint)
    {
        Stop();

        if (!runtime.TryResolveConfiguredPoint(configuredPoint, out var resolvedPoint))
        {
            Fail("Return point is no longer valid. Please choose it again.");
            return;
        }

        targetPoint = resolvedPoint;
        isRunning = true;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
        lastActionAt = DateTime.MinValue;
        transitionedAt = getUtcNow();
        observedTransition = false;
        navigationStarted = false;

        if (resolvedPoint.isInn)
        {
            if (!runtime.TryTeleportToReturnPoint(resolvedPoint, lifestreamIpc))
            {
                Fail("Lifestream is not available, so the inn shortcut cannot be used.");
                return;
            }

            lastActionAt = getUtcNow();
            stage = ReturnStage.WaitingForInn;
            return;
        }

        if (runtime.CanEnterDirectlyFromCurrentLocation(resolvedPoint))
        {
            stage = ReturnStage.MovingToEntrance;
            return;
        }

        if (!runtime.TryTeleportToReturnPoint(resolvedPoint, lifestreamIpc))
        {
            Fail("Could not teleport to the return point.");
            return;
        }

        lastActionAt = getUtcNow();
        stage = ReturnStage.WaitingForTeleport;
    }

    public void Update()
    {
        if (!isRunning || isCompleted || hasFailed || targetPoint == null)
            return;

        switch (stage)
        {
            case ReturnStage.WaitingForTeleport:
                HandleWaitingForTeleport();
                return;
            case ReturnStage.WaitingForInn:
                HandleWaitingForInn();
                return;
            case ReturnStage.MovingToEntrance:
                HandleMovingToEntrance();
                return;
            case ReturnStage.InteractingEntrance:
                HandleInteractingEntrance();
                return;
            case ReturnStage.ConfirmingEntry:
                HandleConfirmingEntry();
                return;
            case ReturnStage.WaitingForIndoor:
                HandleWaitingForIndoor();
                return;
        }
    }

    public void Stop()
    {
        navigationTask.Stop();
        targetPoint = null;
        stage = ReturnStage.Idle;
        lastActionAt = DateTime.MinValue;
        transitionedAt = DateTime.MinValue;
        observedTransition = false;
        navigationStarted = false;
        isRunning = false;
        isCompleted = false;
        hasFailed = false;
        errorMessage = null;
    }

    private void HandleWaitingForTeleport()
    {
        if (targetPoint == null)
        {
            Fail("Return point is missing.");
            return;
        }

        if (runtime.IsTransitioning())
        {
            observedTransition = true;
            return;
        }

        if (runtime.GetCurrentTerritoryId() == targetPoint.territoryId
            && (observedTransition || (getUtcNow() - lastActionAt) > TimeSpan.FromSeconds(2)))
        {
            stage = ReturnStage.MovingToEntrance;
            transitionedAt = getUtcNow();
            return;
        }

        if ((getUtcNow() - transitionedAt) > TimeSpan.FromSeconds(15))
            Fail("Timed out while waiting for residential teleport.");
    }

    private void HandleWaitingForInn()
    {
        if (runtime.IsInsideInn())
        {
            Complete();
            return;
        }

        if (runtime.IsTransitioning() || lifestreamIpc.IsBusy())
        {
            observedTransition = true;
            return;
        }

        if ((getUtcNow() - transitionedAt) > InnTeleportTimeout)
            Fail("Timed out while waiting for inn teleport.");
    }

    private void HandleMovingToEntrance()
    {
        if (targetPoint == null)
        {
            Fail("Return point is missing.");
            return;
        }

        if (runtime.IsInsideHouse())
        {
            Complete();
            return;
        }

        if (!runtime.TryGetHousingEntrance(targetPoint.isApartment, out var entrancePosition))
        {
            if ((getUtcNow() - transitionedAt) > TimeSpan.FromSeconds(15))
            {
                Fail("House entrance not found.");
                return;
            }

            return;
        }

        if (!navigationStarted)
        {
            navigationTask.Start(new NavigationRequest(
                entrancePosition,
                runtime.GetCurrentTerritoryId(),
                "return-entrance",
                arrivalDistance: 3f));
            navigationStarted = true;
            transitionedAt = getUtcNow();
            return;
        }

        navigationTask.Update();

        if (navigationTask.hasFailed)
        {
            Fail(navigationTask.errorMessage ?? "Could not reach the house entrance.");
            return;
        }

        if (!navigationTask.isCompleted)
            return;

        navigationTask.Stop();
        navigationStarted = false;
        transitionedAt = getUtcNow();
        stage = ReturnStage.InteractingEntrance;
    }

    private void HandleInteractingEntrance()
    {
        if (targetPoint == null)
        {
            Fail("Return point is missing.");
            return;
        }

        if (runtime.IsInsideHouse())
        {
            Complete();
            return;
        }

        if ((getUtcNow() - lastActionAt) < TimeSpan.FromSeconds(1))
            return;

        if (!runtime.TryInteractHousingEntrance(targetPoint.isApartment))
        {
            if ((getUtcNow() - transitionedAt) > TimeSpan.FromSeconds(10))
            {
                Fail("Could not interact with the house entrance.");
                return;
            }

            return;
        }

        lastActionAt = getUtcNow();
        transitionedAt = getUtcNow();
        stage = ReturnStage.ConfirmingEntry;
    }

    private void HandleConfirmingEntry()
    {
        if (targetPoint == null)
        {
            Fail("Return point is missing.");
            return;
        }

        if (runtime.IsInsideHouse())
        {
            Complete();
            return;
        }

        if (runtime.TryConfirmEntry(targetPoint.isApartment))
        {
            lastActionAt = getUtcNow();
            transitionedAt = getUtcNow();
            stage = ReturnStage.WaitingForIndoor;
            return;
        }

        if ((getUtcNow() - transitionedAt) > TimeSpan.FromSeconds(6))
        {
            lastActionAt = DateTime.MinValue;
            transitionedAt = getUtcNow();
            stage = ReturnStage.InteractingEntrance;
        }
    }

    private void HandleWaitingForIndoor()
    {
        if (runtime.IsInsideHouse())
        {
            Complete();
            return;
        }

        if ((getUtcNow() - transitionedAt) > TimeSpan.FromSeconds(20))
            Fail("Timed out while waiting to enter the house.");
    }

    private void Complete()
    {
        navigationTask.Stop();
        stage = ReturnStage.Completed;
        isRunning = false;
        isCompleted = true;
        hasFailed = false;
    }

    private void Fail(string message)
    {
        navigationTask.Stop();
        stage = ReturnStage.Failed;
        isRunning = false;
        isCompleted = false;
        hasFailed = true;
        errorMessage = message;
        TryDuoLogError(message);
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

    private sealed class LiveReturnTaskRuntime : IReturnTaskRuntime
    {
        private readonly LocationGame locationGame;
        private readonly NpcGame npcGame;

        public LiveReturnTaskRuntime(LocationGame locationGame, NpcGame npcGame)
        {
            this.locationGame = locationGame;
            this.npcGame = npcGame;
        }

        public bool TryResolveConfiguredPoint(ReturnPointConfig configuredPoint, out ReturnPointConfig resolvedPoint)
        {
            return ReturnPointGame.TryResolveConfiguredPoint(configuredPoint, out resolvedPoint);
        }

        public bool CanEnterDirectlyFromCurrentLocation(ReturnPointConfig point)
        {
            return ReturnPointGame.CanEnterDirectlyFromCurrentLocation(point);
        }

        public bool TryTeleportToReturnPoint(ReturnPointConfig point, ILifestreamIpc lifestreamIpc)
        {
            return ReturnPointGame.TryTeleportToReturnPoint(point, lifestreamIpc);
        }

        public bool IsTransitioning()
        {
            return locationGame.IsTransitioning();
        }

        public bool IsInsideInn()
        {
            return locationGame.IsInsideInn();
        }

        public bool IsInsideHouse()
        {
            return locationGame.IsInsideHouse();
        }

        public uint GetCurrentTerritoryId()
        {
            return locationGame.GetCurrentTerritoryId();
        }

        public bool TryGetHousingEntrance(bool isApartment, out Vector3 entrancePosition)
        {
            entrancePosition = Vector3.Zero;
            var localPlayerPosition = locationGame.GetLocalPlayerPosition();
            if (localPlayerPosition == Vector3.Zero)
                return false;

            if (!ReturnPointGame.TryGetHousingEntrance(localPlayerPosition, isApartment, out var entrance) || entrance == null)
                return false;

            entrancePosition = entrance.Position;
            return true;
        }

        public bool TryInteractHousingEntrance(bool isApartment)
        {
            var localPlayerPosition = locationGame.GetLocalPlayerPosition();
            if (localPlayerPosition == Vector3.Zero)
                return false;

            if (!ReturnPointGame.TryGetHousingEntrance(localPlayerPosition, isApartment, out var entrance) || entrance == null)
                return false;

            return npcGame.TryInteract(entrance, 4f);
        }

        public bool TryConfirmEntry(bool isApartment)
        {
            return ReturnPointGame.TryConfirmEntry(isApartment);
        }
    }
}
