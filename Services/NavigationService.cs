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
    private readonly StateMachine<NavigationState> _stateMachine;

    public enum NavigationState
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

    public NavigationState State { get; private set; } = NavigationState.Idle;
    public string? ErrorMessage { get; private set; }

    private NavigationTarget? _target;
    private DateTime _lastAction = DateTime.MinValue;
    private DateTime _movementStartTime = DateTime.MinValue;
    private DateTime? _localActionReadyAt;
    private bool _teleportAttempted;
    private bool _lifestreamAttempted;

    public NavigationService()
    {
        _stateMachine = new StateMachine<NavigationState>(NavigationState.Idle, state => State = state);
        _stateMachine.Configure(NavigationState.Teleporting, () => HandleTeleport(GetCurrentDistance()));
        _stateMachine.Configure(NavigationState.WaitingForTeleport, HandleWaitingForTeleport);
        _stateMachine.Configure(NavigationState.UsingLifestream, () => HandleLifestream(GetCurrentDistance()));
        _stateMachine.Configure(NavigationState.WaitingForLifestream, HandleWaitingForLifestream);
        _stateMachine.Configure(NavigationState.Pathfinding, HandlePathfinding);
        _stateMachine.Configure(NavigationState.WaitingForArrival, () => HandleWaitForArrival(GetCurrentDistance()));
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
        TransitionTo(NavigationState.Teleporting);
    }

    public void Stop()
    {
        VNavmeshIPC.Stop();
        TransitionTo(NavigationState.Idle);
        _target = null;
        _localActionReadyAt = null;
        ErrorMessage = null;
    }

    public void Update()
    {
        if (State is NavigationState.Idle or NavigationState.Arrived or NavigationState.Failed)
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
            TransitionTo(NavigationState.Arrived);
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
            TransitionTo(NavigationState.UsingLifestream);
    }

    private void HandleWaitingForLifestream()
    {
        if (!LifestreamIPC.IsBusy())
            TransitionTo(NavigationState.Pathfinding);
    }

    private void HandleTeleport(float distance)
    {
        if (_target == null)
            return;

        if (Svc.ClientState.TerritoryType == _target.TerritoryId)
        {
            TransitionTo(_target.IsLifestreamRequired && !_lifestreamAttempted && distance > 40f
                ? NavigationState.UsingLifestream
                : NavigationState.Pathfinding);
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
            TransitionTo(NavigationState.WaitingForTeleport);
        }
    }

    private void HandleLifestream(float distance)
    {
        if (_target == null)
            return;

        if (!_target.IsLifestreamRequired || distance <= 40f)
        {
            TransitionTo(NavigationState.Pathfinding);
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
        TransitionTo(NavigationState.WaitingForLifestream);
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
        TransitionTo(NavigationState.WaitingForArrival);
    }

    private void HandleWaitForArrival(float distance)
    {
        if (_target == null)
            return;

        if (Svc.ClientState.TerritoryType != _target.TerritoryId)
        {
            TransitionTo(NavigationState.Teleporting);
            return;
        }

        if (distance <= _target.ArrivalDistance)
        {
            VNavmeshIPC.Stop();
            TransitionTo(NavigationState.Arrived);
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
        TransitionTo(NavigationState.Failed);
    }

    private void TransitionTo(NavigationState state)
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
