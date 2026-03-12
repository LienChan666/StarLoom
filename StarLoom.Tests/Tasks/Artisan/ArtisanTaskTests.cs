using StarLoom.Config;
using StarLoom.Ipc;
using StarLoom.Tasks.Artisan;
using Xunit;

namespace StarLoom.Tests.Tasks.Artisan;

public sealed class ArtisanTaskTests
{
    [Fact]
    public void Pause_Should_Request_Pause_When_List_Is_Running()
    {
        var artisanIpc = new FakeArtisanIpc
        {
            isAvailable = true,
            isListRunning = true,
            isListPaused = false,
            isBusy = true,
        };

        var pluginConfig = new PluginConfig
        {
            artisanListId = 7,
        };

        var artisanTask = new ArtisanTask(artisanIpc, pluginConfig);

        artisanTask.Pause();

        Assert.True(artisanIpc.pauseRequested);
    }

    [Fact]
    public void RequestPause_Should_Request_Stop_When_List_Is_Running()
    {
        var artisanIpc = new FakeArtisanIpc
        {
            isAvailable = true,
            isListRunning = true,
            isListPaused = false,
            isBusy = true,
        };

        var artisanTask = new ArtisanTask(artisanIpc, new PluginConfig
        {
            artisanListId = 7,
        });

        artisanTask.RequestPause();

        Assert.True(artisanIpc.stopRequestIssued);
        Assert.False(artisanIpc.pauseRequested);
    }

    [Fact]
    public void GetSnapshot_Should_Include_Endurance_Status()
    {
        var artisanIpc = new FakeArtisanIpc
        {
            isAvailable = true,
            isListRunning = false,
            isListPaused = false,
            isBusy = false,
            hasEnduranceEnabled = true,
        };

        var artisanTask = new ArtisanTask(artisanIpc, new PluginConfig
        {
            artisanListId = 7,
        });

        var snapshot = artisanTask.GetSnapshot();

        Assert.True(snapshot.hasEnduranceEnabled);
    }

    private sealed class FakeArtisanIpc : IArtisanIpc
    {
        public bool isAvailable;
        public bool isListRunning;
        public bool isListPaused;
        public bool isBusy;
        public bool hasStopRequest;
        public bool hasEnduranceEnabled;
        public bool pauseRequested;
        public bool stopRequestIssued;

        public bool IsAvailable()
        {
            return isAvailable;
        }

        public bool IsListRunning()
        {
            return isListRunning;
        }

        public bool IsListPaused()
        {
            return isListPaused;
        }

        public bool IsBusy()
        {
            return isBusy;
        }

        public bool GetStopRequest()
        {
            return hasStopRequest;
        }

        public bool GetEnduranceStatus()
        {
            return hasEnduranceEnabled;
        }

        public void SetListPause(bool paused)
        {
            pauseRequested = paused;
            isListPaused = paused;
        }

        public void SetStopRequest(bool stop)
        {
            stopRequestIssued = stop;
            hasStopRequest = stop;
        }

        public void StartListById(int listId)
        {
            isListRunning = true;
        }
    }
}
