using System.Numerics;
using StarLoom.Config;
using StarLoom.Ipc;
using StarLoom.Tasks.Navigation;
using StarLoom.Tasks.Return;
using Xunit;

namespace StarLoom.Tests.Tasks.Return;

public sealed class ReturnTaskTests
{
    [Fact]
    public void Start_Should_Wait_For_Inn_When_Return_Point_Is_Inn()
    {
        var runtime = new FakeReturnTaskRuntime
        {
            resolvedPoint = ReturnPointConfig.CreateInn(),
            teleportResult = true,
        };

        var returnTask = CreateReturnTask(runtime);

        returnTask.Start(ReturnPointConfig.CreateInn());

        Assert.Equal("WaitingForInn", returnTask.currentStage);
        Assert.True(runtime.teleportAttempted);
    }

    [Fact]
    public void Start_Should_Default_To_Inn_When_Return_Point_Is_Null()
    {
        var runtime = new FakeReturnTaskRuntime
        {
            resolvedPoint = ReturnPointConfig.CreateInn(),
            teleportResult = true,
        };

        var returnTask = CreateReturnTask(runtime);

        returnTask.Start(null);

        Assert.Equal("WaitingForInn", returnTask.currentStage);
        Assert.True(runtime.teleportAttempted);
        Assert.True(runtime.configuredPointWasNull);
    }

    [Fact]
    public void Start_Should_Move_To_Entrance_When_Direct_Housing_Entry_Is_Available()
    {
        var runtime = new FakeReturnTaskRuntime
        {
            resolvedPoint = CreateHousingPoint(),
            canEnterDirectly = true,
        };

        var returnTask = CreateReturnTask(runtime);

        returnTask.Start(CreateHousingPoint());

        Assert.Equal("MovingToEntrance", returnTask.currentStage);
        Assert.False(runtime.teleportAttempted);
    }

    [Fact]
    public void Start_Should_Wait_For_Teleport_When_Housing_Requires_Residential_Travel()
    {
        var runtime = new FakeReturnTaskRuntime
        {
            resolvedPoint = CreateHousingPoint(),
            teleportResult = true,
        };

        var returnTask = CreateReturnTask(runtime);

        returnTask.Start(CreateHousingPoint());

        Assert.Equal("WaitingForTeleport", returnTask.currentStage);
        Assert.True(runtime.teleportAttempted);
    }

    private static ReturnTask CreateReturnTask(FakeReturnTaskRuntime runtime)
    {
        return new ReturnTask(
            new NavigationTask(new FakeVNavmeshIpc(), new FakeLifestreamIpc()),
            new FakeLifestreamIpc(),
            runtime,
            () => DateTime.UnixEpoch);
    }

    private static ReturnPointConfig CreateHousingPoint()
    {
        return new ReturnPointConfig
        {
            territoryId = 339,
            aetheryteId = 75,
            displayName = "Lavender Beds",
        };
    }

    private sealed class FakeReturnTaskRuntime : IReturnTaskRuntime
    {
        public ReturnPointConfig resolvedPoint = ReturnPointConfig.CreateInn();
        public bool canEnterDirectly;
        public bool teleportResult;
        public bool teleportAttempted;
        public bool configuredPointWasNull;

        public bool TryResolveConfiguredPoint(ReturnPointConfig? configuredPoint, out ReturnPointConfig resolvedPoint)
        {
            configuredPointWasNull = configuredPoint == null;
            resolvedPoint = this.resolvedPoint;
            return true;
        }

        public bool CanEnterDirectlyFromCurrentLocation(ReturnPointConfig point)
        {
            return canEnterDirectly;
        }

        public bool TryTeleportToReturnPoint(ReturnPointConfig point, ILifestreamIpc lifestreamIpc)
        {
            teleportAttempted = true;
            return teleportResult;
        }

        public bool IsTransitioning()
        {
            return false;
        }

        public bool IsInsideInn()
        {
            return false;
        }

        public bool IsInsideHouse()
        {
            return false;
        }

        public uint GetCurrentTerritoryId()
        {
            return resolvedPoint.territoryId;
        }

        public bool TryGetHousingEntrance(bool isApartment, out Vector3 entrancePosition)
        {
            entrancePosition = new Vector3(1f, 0f, 1f);
            return true;
        }

        public bool TryInteractHousingEntrance(bool isApartment)
        {
            return true;
        }

        public bool TryConfirmEntry(bool isApartment)
        {
            return true;
        }
    }

    private sealed class FakeVNavmeshIpc : IVNavmeshIpc
    {
        public bool IsAvailable() => true;
        public bool PathfindAndMoveTo(Vector3 destination, bool fly) => true;
        public bool IsPathRunning() => false;
        public void Stop() { }
    }

    private sealed class FakeLifestreamIpc : ILifestreamIpc
    {
        public bool IsAvailable() => true;
        public bool IsBusy() => false;
        public void ExecuteCommand(string command) { }
        public void Abort() { }
        public void EnqueueInnShortcut(int? mode = null) { }
    }
}
