using ECommons.DalamudServices;
using Starloom.Automation;
using Starloom.GameInterop.IPC;
using System;
using System.Numerics;

namespace Starloom.Services;

public enum NavigationStatus
{
    Idle,
    Teleporting,
    WaitingForTeleport,
    UsingLifestream,
    WaitingForLifestream,
    Pathfinding,
    WaitingForArrival,
    Arrived,
    Failed,
}

public record NavigationTarget(
    Vector3 Location,
    uint AetheryteId,
    uint TerritoryId,
    float ArrivalDistance = 2f,
    bool IsLifestreamRequired = false,
    string LifestreamCommand = "");

public sealed class NavigationService
{
    private static readonly TimeSpan LocalActionReadyStableDuration = TimeSpan.FromMilliseconds(500);
    private readonly StateMachine<NavigationStatus> stateMachine;

    public NavigationStatus State { get; private set; } = NavigationStatus.Idle;
    public string? ErrorMessage { get; private set; }

    private NavigationTarget? target;
    private DateTime lastAction = DateTime.MinValue;
    private DateTime movementStartTime = DateTime.MinValue;
    private DateTime? localActionReadyAt;
    private bool teleportAttempted;
    private bool lifestreamAttempted;

    public NavigationService()
    {
        stateMachine = new StateMachine<NavigationStatus>(NavigationStatus.Idle, state => State = state);
        stateMachine.Configure(NavigationStatus.Teleporting, () => HandleTeleport(GetCurrentDistance()));
        stateMachine.Configure(NavigationStatus.WaitingForTeleport, HandleWaitingForTeleport);
        stateMachine.Configure(NavigationStatus.UsingLifestream, () => HandleLifestream(GetCurrentDistance()));
        stateMachine.Configure(NavigationStatus.WaitingForLifestream, HandleWaitingForLifestream);
        stateMachine.Configure(NavigationStatus.Pathfinding, HandlePathfinding);
        stateMachine.Configure(NavigationStatus.WaitingForArrival, () => HandleWaitForArrival(GetCurrentDistance()));
    }

    public void NavigateTo(NavigationTarget target)
    {
        this.target = target;
        teleportAttempted = false;
        lifestreamAttempted = false;
        lastAction = DateTime.MinValue;
        movementStartTime = DateTime.MinValue;
        localActionReadyAt = null;
        ErrorMessage = null;
        TransitionTo(NavigationStatus.Teleporting);
    }

    public void Stop()
    {
        VNavmeshIpc.Stop();
        TransitionTo(NavigationStatus.Idle);
        target = null;
        localActionReadyAt = null;
        ErrorMessage = null;
    }

    public void Update()
    {
        if (State is NavigationStatus.Idle or NavigationStatus.Arrived or NavigationStatus.Failed)
            return;

        if (target == null)
        {
            Fail("Navigation target is missing.");
            return;
        }

        if (!HasStableLocalControl())
            return;

        var distance = GetCurrentDistance();
        if (Svc.ClientState.TerritoryType == target.TerritoryId && distance <= target.ArrivalDistance)
        {
            VNavmeshIpc.Stop();
            TransitionTo(NavigationStatus.Arrived);
            return;
        }

        stateMachine.Update();
    }

    private float GetCurrentDistance()
    {
        if (target == null)
            return float.MaxValue;

        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (playerPos == Vector3.Zero)
            return float.MaxValue;

        return Svc.ClientState.TerritoryType == target.TerritoryId
            ? Vector3.Distance(playerPos, target.Location)
            : float.MaxValue;
    }

    private void HandleWaitingForTeleport()
    {
        if ((DateTime.UtcNow - lastAction) > TimeSpan.FromSeconds(5))
            TransitionTo(NavigationStatus.UsingLifestream);
    }

    private void HandleWaitingForLifestream()
    {
        if (!LifestreamIpc.IsBusy())
            TransitionTo(NavigationStatus.Pathfinding);
    }

    private void HandleTeleport(float distance)
    {
        if (target == null)
            return;

        if (Svc.ClientState.TerritoryType == target.TerritoryId)
        {
            TransitionTo(target.IsLifestreamRequired && !lifestreamAttempted && distance > 40f
                ? NavigationStatus.UsingLifestream
                : NavigationStatus.Pathfinding);
            return;
        }

        if (!teleportAttempted)
        {
            if (!NativeTeleporter.Teleport(target.AetheryteId))
            {
                Fail("Teleport failed.");
                return;
            }

            teleportAttempted = true;
            lastAction = DateTime.UtcNow;
            TransitionTo(NavigationStatus.WaitingForTeleport);
        }
    }

    private void HandleLifestream(float distance)
    {
        if (target == null)
            return;

        if (!target.IsLifestreamRequired || distance <= 40f)
        {
            TransitionTo(NavigationStatus.Pathfinding);
            return;
        }

        if (!LifestreamIpc.IsAvailable())
        {
            Fail("Lifestream is required.");
            return;
        }

        LifestreamIpc.ExecuteCommand(target.LifestreamCommand);
        lifestreamAttempted = true;
        lastAction = DateTime.UtcNow;
        TransitionTo(NavigationStatus.WaitingForLifestream);
    }

    private void HandlePathfinding()
    {
        if (target == null)
            return;

        if (!VNavmeshIpc.IsPathRunning())
        {
            if (!VNavmeshIpc.PathfindAndMoveTo(target.Location, false))
            {
                Fail("Pathfinding failed.");
                return;
            }

            movementStartTime = DateTime.UtcNow;
        }

        lastAction = DateTime.UtcNow;
        TransitionTo(NavigationStatus.WaitingForArrival);
    }

    private void HandleWaitForArrival(float distance)
    {
        if (target == null)
            return;

        if (Svc.ClientState.TerritoryType != target.TerritoryId)
        {
            TransitionTo(NavigationStatus.Teleporting);
            return;
        }

        if (distance <= target.ArrivalDistance)
        {
            VNavmeshIpc.Stop();
            TransitionTo(NavigationStatus.Arrived);
            return;
        }

        if ((DateTime.UtcNow - movementStartTime) > TimeSpan.FromSeconds(60))
        {
            Fail($"Navigation timed out. Distance to target: {distance:F1}m.");
            return;
        }

        if (!VNavmeshIpc.IsPathRunning() && (DateTime.UtcNow - lastAction) > TimeSpan.FromMilliseconds(500))
        {
            VNavmeshIpc.PathfindAndMoveTo(target.Location, false);
            lastAction = DateTime.UtcNow;
        }
    }

    private void Fail(string message)
    {
        Svc.Log.Error($"Navigation failed: {message} (territory={Svc.ClientState.TerritoryType}, target={target?.TerritoryId})");
        ErrorMessage = message;
        localActionReadyAt = null;
        VNavmeshIpc.Stop();
        TransitionTo(NavigationStatus.Failed);
    }

    private void TransitionTo(NavigationStatus state)
        => stateMachine.TransitionTo(state);

    private bool HasStableLocalControl()
    {
        var localStatus = LocalPlayerActionGate.GetStatus();
        if (!LocalPlayerActionGate.IsReadyForAutomation(localStatus))
        {
            localActionReadyAt = null;
            return false;
        }

        if (localActionReadyAt is null)
        {
            localActionReadyAt = DateTime.UtcNow;
            return false;
        }

        return (DateTime.UtcNow - localActionReadyAt.Value) >= LocalActionReadyStableDuration;
    }
}
