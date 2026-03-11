using System.Numerics;
using StarLoom.Ipc;
using StarLoom.Tasks.Navigation;
using Xunit;

namespace StarLoom.Tests.Tasks.Navigation;

public sealed class NavigationTaskTests
{
    [Fact]
    public void Update_Should_Complete_When_External_Navigation_Reports_Arrival()
    {
        var vNavmeshIpc = new FakeVNavmeshIpc
        {
            isAvailable = true,
            pathfindResult = true,
            isPathRunning = false,
        };

        var lifestreamIpc = new FakeLifestreamIpc();
        var navigationTask = new NavigationTask(vNavmeshIpc, lifestreamIpc);

        navigationTask.Start(new NavigationRequest(new Vector3(1f, 2f, 3f), 1, "collectable"));
        navigationTask.Update();

        Assert.True(navigationTask.isCompleted);
        Assert.False(navigationTask.isRunning);
        Assert.False(navigationTask.hasFailed);
    }

    private sealed class FakeVNavmeshIpc : IVNavmeshIpc
    {
        public bool isAvailable;
        public bool pathfindResult;
        public bool isPathRunning;

        public bool IsAvailable()
        {
            return isAvailable;
        }

        public bool PathfindAndMoveTo(Vector3 destination, bool fly)
        {
            return pathfindResult;
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
        public bool IsAvailable()
        {
            return true;
        }

        public bool IsBusy()
        {
            return false;
        }

        public void ExecuteCommand(string command)
        {
        }

        public void Abort()
        {
        }

        public void EnqueueInnShortcut(int? mode = null)
        {
        }
    }
}
