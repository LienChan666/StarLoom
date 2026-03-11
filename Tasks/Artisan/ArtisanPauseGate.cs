namespace StarLoom.Tasks.Artisan;

public enum ArtisanPauseGatePhase
{
    WaitingForAcknowledgement,
    WaitingForIdle,
}

public enum ArtisanPauseDecisionKind
{
    ContinueWaiting,
    MoveToIdleWait,
    Fail,
}

public readonly record struct ArtisanPauseStatus(
    bool isBusy,
    bool isListRunning,
    bool isPaused,
    bool hasStopRequest);

public readonly record struct ArtisanPauseDecision(
    ArtisanPauseDecisionKind kind,
    string? errorMessage = null);

public static class ArtisanPauseGate
{
    public static bool HasPauseAcknowledgement(ArtisanPauseStatus status)
    {
        return status.isPaused || !status.isListRunning;
    }

    public static ArtisanPauseDecision Evaluate(
        ArtisanPauseGatePhase phase,
        ArtisanPauseStatus status,
        TimeSpan elapsed,
        TimeSpan acknowledgementTimeout,
        TimeSpan idleTimeout)
    {
        return phase switch
        {
            ArtisanPauseGatePhase.WaitingForAcknowledgement => EvaluateAcknowledgement(status, elapsed, acknowledgementTimeout),
            ArtisanPauseGatePhase.WaitingForIdle => EvaluateIdle(elapsed, idleTimeout),
            _ => new ArtisanPauseDecision(ArtisanPauseDecisionKind.Fail, "Unsupported Artisan pause gate phase."),
        };
    }

    private static ArtisanPauseDecision EvaluateAcknowledgement(
        ArtisanPauseStatus status,
        TimeSpan elapsed,
        TimeSpan acknowledgementTimeout)
    {
        if (HasPauseAcknowledgement(status))
            return new ArtisanPauseDecision(ArtisanPauseDecisionKind.MoveToIdleWait);

        if (elapsed > acknowledgementTimeout)
            return new ArtisanPauseDecision(
                ArtisanPauseDecisionKind.Fail,
                "Timed out while waiting for Artisan pause acknowledgement.");

        return new ArtisanPauseDecision(ArtisanPauseDecisionKind.ContinueWaiting);
    }

    private static ArtisanPauseDecision EvaluateIdle(TimeSpan elapsed, TimeSpan idleTimeout)
    {
        if (elapsed > idleTimeout)
            return new ArtisanPauseDecision(
                ArtisanPauseDecisionKind.Fail,
                "Timed out while waiting for local control after pausing Artisan.");

        return new ArtisanPauseDecision(ArtisanPauseDecisionKind.ContinueWaiting);
    }
}
