using Starloom.Automation;
using System;

namespace Starloom.Tasks;

internal static class TaskArtisanPause
{
    private static readonly TimeSpan PauseAcknowledgementTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ArtisanIdleTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LocalActionReadyStableDuration = TimeSpan.FromMilliseconds(500);

    private static DateTime stateEnteredAt;
    private static DateTime idleWaitEnteredAt;
    private static DateTime? localActionReadyAt;

    internal static void EnqueueIfNeeded()
    {
        if (!P.Artisan.IsAvailable())
            return;

        if (!P.Artisan.IsListRunning() && !P.Artisan.GetEnduranceStatus())
            return;

        P.Artisan.SetStopRequest(true);
        stateEnteredAt = DateTime.UtcNow;
        idleWaitEnteredAt = DateTime.MinValue;
        localActionReadyAt = null;

        P.TM.Enqueue(WaitForPauseAcknowledgement, "ArtisanPause.WaitAck");
        P.TM.Enqueue(WaitForArtisanIdle, "ArtisanPause.WaitIdle");
    }

    private static bool? WaitForPauseAcknowledgement()
    {
        var core = P.Artisan.GetPauseStatus();
        var status = new ArtisanPauseStatus(core.IsBusy, core.IsListRunning, core.IsListPaused, core.StopRequested, core.EnduranceEnabled);
        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForAcknowledgement,
            status,
            DateTime.UtcNow - stateEnteredAt,
            PauseAcknowledgementTimeout,
            ArtisanIdleTimeout);

        return decision.Kind switch
        {
            ArtisanPauseDecisionKind.MoveToIdleWait => true,
            ArtisanPauseDecisionKind.Fail => null,
            _ => false,
        };
    }

    private static bool? WaitForArtisanIdle()
    {
        if (idleWaitEnteredAt == DateTime.MinValue)
            idleWaitEnteredAt = DateTime.UtcNow;

        var core = P.Artisan.GetPauseStatus();
        var artisanStatus = new ArtisanPauseStatus(core.IsBusy, core.IsListRunning, core.IsListPaused, core.StopRequested, core.EnduranceEnabled);
        var localStatus = LocalPlayerActionGate.GetStatus();

        if (ArtisanPauseGate.HasPauseAcknowledgement(artisanStatus)
            && LocalPlayerActionGate.IsReadyForAutomation(localStatus))
        {
            if (localActionReadyAt is null)
            {
                localActionReadyAt = DateTime.UtcNow;
                return false;
            }

            if ((DateTime.UtcNow - localActionReadyAt.Value) >= LocalActionReadyStableDuration)
            {
                localActionReadyAt = null;
                return true;
            }
        }
        else
        {
            localActionReadyAt = null;
        }

        var decision = ArtisanPauseGate.Evaluate(
            ArtisanPauseGatePhase.WaitingForIdle,
            artisanStatus,
            DateTime.UtcNow - idleWaitEnteredAt,
            PauseAcknowledgementTimeout,
            ArtisanIdleTimeout);

        if (decision.Kind == ArtisanPauseDecisionKind.Fail)
            return null;

        return false;
    }
}
