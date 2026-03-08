using ECommons.DalamudServices;
using Starloom.Core;
using Starloom.IPC;
using System;
using System.Numerics;

namespace Starloom.Services;

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

    public void NavigateTo(NavigationTarget target)
    {
        _target = target;
        _teleportAttempted = false;
        _lifestreamAttempted = false;
        _lastAction = DateTime.MinValue;
        _movementStartTime = DateTime.MinValue;
        _localActionReadyAt = null;
        ErrorMessage = null;
        State = NavigationState.Teleporting;
    }

    public void Stop()
    {
        VNavmeshIPC.Stop();
        State = NavigationState.Idle;
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
            State = NavigationState.Failed;
            return;
        }

        if (!HasStableLocalControl())
            return;

        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (playerPos == Vector3.Zero)
            return;

        var distance = Svc.ClientState.TerritoryType == _target.TerritoryId
            ? Vector3.Distance(playerPos, _target.Location)
            : float.MaxValue;

        if (Svc.ClientState.TerritoryType == _target.TerritoryId && distance <= _target.ArrivalDistance)
        {
            VNavmeshIPC.Stop();
            State = NavigationState.Arrived;
            return;
        }

        switch (State)
        {
            case NavigationState.Teleporting:
                HandleTeleport(distance);
                break;
            case NavigationState.WaitingForTeleport:
                if ((DateTime.UtcNow - _lastAction) > TimeSpan.FromSeconds(5))
                    State = NavigationState.UsingLifestream;
                break;
            case NavigationState.UsingLifestream:
                HandleLifestream(distance);
                break;
            case NavigationState.WaitingForLifestream:
                if (!LifestreamIPC.IsBusy())
                    State = NavigationState.Pathfinding;
                break;
            case NavigationState.Pathfinding:
                HandlePathfinding();
                break;
            case NavigationState.WaitingForArrival:
                HandleWaitForArrival(distance);
                break;
        }
    }

    private void HandleTeleport(float distance)
    {
        if (Svc.ClientState.TerritoryType == _target!.TerritoryId)
        {
            State = _target.IsLifestreamRequired && !_lifestreamAttempted && distance > 40f
                ? NavigationState.UsingLifestream
                : NavigationState.Pathfinding;
            return;
        }

        if (!_teleportAttempted)
        {
            if (!NativeTeleporter.Teleport(_target.AetheryteId))
            {
                Fail("传送失败");
                return;
            }

            _teleportAttempted = true;
            _lastAction = DateTime.UtcNow;
            State = NavigationState.WaitingForTeleport;
        }
    }

    private void HandleLifestream(float distance)
    {
        if (_target == null)
            return;

        if (!_target.IsLifestreamRequired || distance <= 40f)
        {
            State = NavigationState.Pathfinding;
            return;
        }

        if (!LifestreamIPC.IsAvailable())
        {
            Fail("需要 Lifestream 插件");
            return;
        }

        LifestreamIPC.ExecuteCommand(_target.LifestreamCommand);
        _lifestreamAttempted = true;
        _lastAction = DateTime.UtcNow;
        State = NavigationState.WaitingForLifestream;
    }

    private void HandlePathfinding()
    {
        if (!VNavmeshIPC.IsPathRunning())
        {
            if (!VNavmeshIPC.PathfindAndMoveTo(_target!.Location, false))
            {
                Fail("寻路失败");
                return;
            }

            _movementStartTime = DateTime.UtcNow;
        }

        _lastAction = DateTime.UtcNow;
        State = NavigationState.WaitingForArrival;
    }

    private void HandleWaitForArrival(float distance)
    {
        if (Svc.ClientState.TerritoryType != _target!.TerritoryId)
        {
            State = NavigationState.Teleporting;
            return;
        }

        if (distance <= _target.ArrivalDistance)
        {
            VNavmeshIPC.Stop();
            State = NavigationState.Arrived;
            return;
        }

        if ((DateTime.UtcNow - _movementStartTime) > TimeSpan.FromSeconds(60))
        {
            Fail($"导航超时，距目标 {distance:F1}m");
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
        State = NavigationState.Failed;
    }

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
