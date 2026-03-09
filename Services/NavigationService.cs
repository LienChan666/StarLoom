using ECommons.DalamudServices;
using StarLoom.Core;
using StarLoom.IPC;
using StarLoom.Services.Interfaces;
using System;
using System.Numerics;

namespace StarLoom.Services;

public record NavigationTarget(
    Vector3 Location,
    uint AetheryteId,
    uint TerritoryId,
    float ArrivalDistance = 2f,
    bool IsLifestreamRequired = false,
    string LifestreamCommand = "");

public sealed class NavigationService : INavigationService
{
    private static readonly TimeSpan LocalActionReadyStableDuration = TimeSpan.FromMilliseconds(500);
    private readonly StateMachine<NavigationStatus> _stateMachine;

    public NavigationStatus State { get; private set; } = NavigationStatus.Idle;
    public string? ErrorMessage { get; private set; }

    private NavigationTarget? _target;
    private DateTime _lastAction = DateTime.MinValue;
    private DateTime _movementStartTime = DateTime.MinValue;
    private DateTime? _localActionReadyAt;
    private bool _teleportAttempted;
    private bool _lifestreamAttempted;

    public NavigationService()
    {
        _stateMachine = new StateMachine<NavigationStatus>(NavigationStatus.Idle, state => State = state);
        _stateMachine.Configure(NavigationStatus.Teleporting, () => HandleTeleport(GetCurrentDistance()));
        _stateMachine.Configure(NavigationStatus.WaitingForTeleport, HandleWaitingForTeleport);
        _stateMachine.Configure(NavigationStatus.UsingLifestream, () => HandleLifestream(GetCurrentDistance()));
        _stateMachine.Configure(NavigationStatus.WaitingForLifestream, HandleWaitingForLifestream);
        _stateMachine.Configure(NavigationStatus.Pathfinding, HandlePathfinding);
        _stateMachine.Configure(NavigationStatus.WaitingForArrival, () => HandleWaitForArrival(GetCurrentDistance()));
    }

    public void NavigateTo(NavigationTarget target)
    {
        _target = target;
        _teleportAttempted = false;
        _lifestreamAttempted = false;
        _lastAction = DateTime.MinValue;
        _movementStartTime = DateTime.MinValue;
        _localActionReadyAt = null;
        ErrorMessage = null;
        TransitionTo(NavigationStatus.Teleporting);
    }

    public void Stop()
    {
        VNavmeshIPC.Stop();
        TransitionTo(NavigationStatus.Idle);
        _target = null;
        _localActionReadyAt = null;
        ErrorMessage = null;
    }

    public void Update()
    {
        if (State is NavigationStatus.Idle or NavigationStatus.Arrived or NavigationStatus.Failed)
            return;

        if (_target == null)
        {
            Fail("Navigation target is missing.");
            return;
        }

        if (!HasStableLocalControl())
            return;

        var distance = GetCurrentDistance();
        if (Svc.ClientState.TerritoryType == _target.TerritoryId && distance <= _target.ArrivalDistance)
        {
            VNavmeshIPC.Stop();
            TransitionTo(NavigationStatus.Arrived);
            return;
        }

        _stateMachine.Update();
    }

    private float GetCurrentDistance()
    {
        if (_target == null)
            return float.MaxValue;

        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (playerPos == Vector3.Zero)
            return float.MaxValue;

        return Svc.ClientState.TerritoryType == _target.TerritoryId
            ? Vector3.Distance(playerPos, _target.Location)
            : float.MaxValue;
    }

    private void HandleWaitingForTeleport()
    {
        if ((DateTime.UtcNow - _lastAction) > TimeSpan.FromSeconds(5))
            TransitionTo(NavigationStatus.UsingLifestream);
    }

    private void HandleWaitingForLifestream()
    {
        if (!LifestreamIPC.IsBusy())
            TransitionTo(NavigationStatus.Pathfinding);
    }

    private void HandleTeleport(float distance)
    {
        if (_target == null)
            return;

        if (Svc.ClientState.TerritoryType == _target.TerritoryId)
        {
            TransitionTo(_target.IsLifestreamRequired && !_lifestreamAttempted && distance > 40f
                ? NavigationStatus.UsingLifestream
                : NavigationStatus.Pathfinding);
            return;
        }

        if (!_teleportAttempted)
        {
            if (!NativeTeleporter.Teleport(_target.AetheryteId))
            {
                Fail("Teleport failed.");
                return;
            }

            _teleportAttempted = true;
            _lastAction = DateTime.UtcNow;
            TransitionTo(NavigationStatus.WaitingForTeleport);
        }
    }

    private void HandleLifestream(float distance)
    {
        if (_target == null)
            return;

        if (!_target.IsLifestreamRequired || distance <= 40f)
        {
            TransitionTo(NavigationStatus.Pathfinding);
            return;
        }

        if (!LifestreamIPC.IsAvailable())
        {
            Fail("Lifestream is required.");
            return;
        }

        LifestreamIPC.ExecuteCommand(_target.LifestreamCommand);
        _lifestreamAttempted = true;
        _lastAction = DateTime.UtcNow;
        TransitionTo(NavigationStatus.WaitingForLifestream);
    }

    private void HandlePathfinding()
    {
        if (_target == null)
            return;

        if (!VNavmeshIPC.IsPathRunning())
        {
            if (!VNavmeshIPC.PathfindAndMoveTo(_target.Location, false))
            {
                Fail("Pathfinding failed.");
                return;
            }

            _movementStartTime = DateTime.UtcNow;
        }

        _lastAction = DateTime.UtcNow;
            TransitionTo(NavigationStatus.WaitingForArrival);
    }

    private void HandleWaitForArrival(float distance)
    {
        if (_target == null)
            return;

        if (Svc.ClientState.TerritoryType != _target.TerritoryId)
        {
            TransitionTo(NavigationStatus.Teleporting);
            return;
        }

        if (distance <= _target.ArrivalDistance)
        {
            VNavmeshIPC.Stop();
            TransitionTo(NavigationStatus.Arrived);
            return;
        }

        if ((DateTime.UtcNow - _movementStartTime) > TimeSpan.FromSeconds(60))
        {
            Fail($"Navigation timed out. Distance to target: {distance:F1}m.");
            return;
        }

        if (!VNavmeshIPC.IsPathRunning() && (DateTime.UtcNow - _lastAction) > TimeSpan.FromMilliseconds(500))
        {
            VNavmeshIPC.PathfindAndMoveTo(_target.Location, false);
            _lastAction = DateTime.UtcNow;
        }
    }

    private void Fail(string message)
    {
        ErrorMessage = message;
        _localActionReadyAt = null;
        VNavmeshIPC.Stop();
        TransitionTo(NavigationStatus.Failed);
    }

    private void TransitionTo(NavigationStatus state)
        => _stateMachine.TransitionTo(state);

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
