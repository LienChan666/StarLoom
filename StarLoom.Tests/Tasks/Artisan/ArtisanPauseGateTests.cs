using StarLoom.Tasks.Artisan;
using Xunit;

namespace StarLoom.Tests.Tasks.Artisan;

public sealed class ArtisanPauseGateTests
{
    [Fact]
    public void Evaluate_Should_Move_To_Idle_Wait_When_Pause_Is_Acknowledged()
    {
        var status = new ArtisanPauseStatus(
            isBusy: true,
            isListRunning: true,
            isPaused: true,
            hasStopRequest: true);

        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForAcknowledgement,
            status,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15));

        Assert.Equal(ArtisanPauseDecisionKind.MoveToIdleWait, decision.kind);
    }

    [Fact]
    public void Evaluate_Should_Fail_When_Acknowledgement_Times_Out()
    {
        var status = new ArtisanPauseStatus(
            isBusy: true,
            isListRunning: true,
            isPaused: false,
            hasStopRequest: true);

        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForAcknowledgement,
            status,
            TimeSpan.FromSeconds(6),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15));

        Assert.Equal(ArtisanPauseDecisionKind.Fail, decision.kind);
        Assert.Equal("Timed out while waiting for Artisan pause acknowledgement.", decision.errorMessage);
    }

    [Fact]
    public void Evaluate_Should_Fail_When_Idle_Wait_Times_Out()
    {
        var status = new ArtisanPauseStatus(
            isBusy: false,
            isListRunning: true,
            isPaused: true,
            hasStopRequest: true);

        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForIdle,
            status,
            TimeSpan.FromSeconds(16),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15));

        Assert.Equal(ArtisanPauseDecisionKind.Fail, decision.kind);
        Assert.Equal("Timed out while waiting for local control after pausing Artisan.", decision.errorMessage);
    }

    [Fact]
    public void HasPauseAcknowledgement_Should_Return_True_When_List_Stops()
    {
        var status = new ArtisanPauseStatus(
            isBusy: false,
            isListRunning: false,
            isPaused: false,
            hasStopRequest: true);

        Assert.True(ArtisanPauseGate.HasPauseAcknowledgement(status));
    }
}
