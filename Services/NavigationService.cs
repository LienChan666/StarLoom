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
    private NavigationStatus status = NavigationStatus.Idle;
    public string? ErrorMessage { get; private set; }
    public bool IsIdle => status == NavigationStatus.Idle;
    public bool IsComplete => status == NavigationStatus.Arrived;
    public bool HasFailed => status == NavigationStatus.Failed;

    private NavigationTarget? target;
    private DateTime lastAction = DateTime.MinValue;
    private DateTime movementStartTime = DateTime.MinValue;
    private DateTime? _localActionReadyAt;
    private bool teleportAttempted;
    private bool lifestreamAttempted;

    public void NavigateTo(NavigationTarget target)
    {
        this.target = target;
        teleportAttempted = false;
        lifestreamAttempted = false;
        lastAction = DateTime.MinValue;
        movementStartTime = DateTime.MinValue;
        _localActionReadyAt = null;
        ErrorMessage = null;
        TransitionTo(NavigationStatus.Teleporting);
    }

    public void Stop()
    {
        VNavmeshIpc.Stop();
        TransitionTo(NavigationStatus.Idle);
        target = null;
        _localActionReadyAt = null;
        ErrorMessage = null;
    }

    public void Poll()
    {
        if (status is NavigationStatus.Idle or NavigationStatus.Arrived or NavigationStatus.Failed)
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

        switch (status)
        {
            case NavigationStatus.Teleporting:
                HandleTeleport(distance);
                return;
            case NavigationStatus.WaitingForTeleport:
                HandleWaitingForTeleport();
                return;
            case NavigationStatus.UsingLifestream:
                HandleLifestream(distance);
                return;
            case NavigationStatus.WaitingForLifestream:
                HandleWaitingForLifestream();
                return;
            case NavigationStatus.Pathfinding:
                HandlePathfinding();
                return;
            case NavigationStatus.WaitingForArrival:
                HandleWaitForArrival(distance);
                return;
        }
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
        _localActionReadyAt = null;
        VNavmeshIpc.Stop();
        TransitionTo(NavigationStatus.Failed);
    }

    private void TransitionTo(NavigationStatus state)
        => status = state;

    private bool HasStableLocalControl()
    {
        var localStatus = LocalPlayerActionGate.GetStatus();
        if (!LocalPlayerActionGate.IsReadyForAutomation(localStatus))
        {
            _localActionReadyAt = null;
            return false;
        }

        if (_localActionReadyAt is null)
        {
            _localActionReadyAt = DateTime.UtcNow;
            return false;
        }

        return (DateTime.UtcNow - _localActionReadyAt.Value) >= LocalActionReadyStableDuration;
    }
}
