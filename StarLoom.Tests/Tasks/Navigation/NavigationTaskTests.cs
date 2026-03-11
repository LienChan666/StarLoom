using System.Numerics;
using StarLoom.Ipc;
using StarLoom.Tasks.Navigation;
using Xunit;

namespace StarLoom.Tests.Tasks.Navigation;

public sealed class NavigationTaskTests
{
    [Fact]
    public void NavigationRequest_Should_Expose_Expanded_Travel_Metadata()
    {
        var parameterNames = typeof(NavigationRequest)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("destination", parameterNames);
        Assert.Contains("territoryId", parameterNames);
        Assert.Contains("aetheryteId", parameterNames);
        Assert.Contains("arrivalDistance", parameterNames);
        Assert.NotNull(typeof(NavigationRequest).GetProperty("aetheryteId"));
        Assert.NotNull(typeof(NavigationRequest).GetProperty("arrivalDistance"));
    }

    [Fact]
    public void Update_Should_Transition_To_WaitingForTeleport_When_Target_Territory_Differs()
    {
        var vNavmeshIpc = new FakeVNavmeshIpc();
        var lifestreamIpc = new FakeLifestreamIpc();
        var snapshot = new NavigationRuntimeSnapshot(999, new Vector3(100f, 0f, 100f), true);
        var now = DateTime.UnixEpoch;
        uint? teleportedAetheryteId = null;

        var navigationTask = new NavigationTask(
            vNavmeshIpc,
            lifestreamIpc,
            () => snapshot,
            (aetheryteId, _) =>
            {
                teleportedAetheryteId = aetheryteId;
                return true;
            },
            () => now);

        navigationTask.Start(new NavigationRequest(
            new Vector3(10f, 0f, 20f),
            1,
            "collectable",
            aetheryteId: 77,
            arrivalDistance: 2f));

        navigationTask.Update();
        now += TimeSpan.FromSeconds(1);
        navigationTask.Update();

        Assert.Equal((uint)77, teleportedAetheryteId);
        Assert.Equal("WaitingForTeleport", navigationTask.currentStage);
        Assert.True(navigationTask.isRunning);
    }

    [Fact]
    public void Update_Should_Transition_To_WaitingForLifestream_When_Request_Uses_Lifestream()
    {
        var vNavmeshIpc = new FakeVNavmeshIpc
        {
            isAvailable = true,
            isPathRunning = true,
        };

        var lifestreamIpc = new FakeLifestreamIpc
        {
            isAvailable = true,
            isBusy = true,
        };

        var snapshot = new NavigationRuntimeSnapshot(1, new Vector3(100f, 0f, 100f), true);
        var now = DateTime.UnixEpoch;
        var navigationTask = new NavigationTask(
            vNavmeshIpc,
            lifestreamIpc,
            () => snapshot,
            (_, _) => true,
            () => now);

        navigationTask.Start(new NavigationRequest(
            new Vector3(0f, 0f, 0f),
            1,
            "collectable",
            aetheryteId: 55,
            arrivalDistance: 2f,
            useLifestream: true,
            lifestreamCommand: "/li house"));

        navigationTask.Update();
        now += TimeSpan.FromSeconds(1);
        navigationTask.Update();
        navigationTask.Update();

        Assert.Equal("/li house", lifestreamIpc.lastCommand);
        Assert.Equal("WaitingForLifestream", navigationTask.currentStage);
    }

    private sealed class FakeVNavmeshIpc : IVNavmeshIpc
    {
        public bool isAvailable = true;
        public bool isPathRunning;

        public bool IsAvailable()
        {
            return isAvailable;
        }

        public bool PathfindAndMoveTo(Vector3 destination, bool fly)
        {
            return true;
        }

        public bool IsPathRunning()
        {
            return isPathRunning;
        }

        public void Stop()
        {
        }
    }

    private sealed class FakeLifestreamIpc : ILifestreamIpc
    {
        public bool isAvailable = true;
        public bool isBusy;
        public string lastCommand = string.Empty;

        public bool IsAvailable()
        {
            return isAvailable;
        }

        public bool IsBusy()
        {
            return isBusy;
        }

        public void ExecuteCommand(string command)
        {
            lastCommand = command;
        }

        public void Abort()
        {
        }

        public void EnqueueInnShortcut(int? mode = null)
        {
        }
    }
}
