using System;

namespace Starloom.Core;

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
    bool IsBusy,
    bool IsListRunning,
    bool IsListPaused,
    bool StopRequested,
    bool EnduranceEnabled);

public readonly record struct ArtisanPauseDecision(ArtisanPauseDecisionKind Kind, string? FailureMessage = null);

public static class ArtisanPauseGate
{
    public static bool HasPauseAcknowledgement(ArtisanPauseStatus status)
        => status.IsListPaused || !status.IsListRunning;

    public static ArtisanPauseDecision Evaluate(
        ArtisanPauseGatePhase phase,
        ArtisanPauseStatus status,
        TimeSpan elapsed,
        TimeSpan acknowledgementTimeout,
        TimeSpan idleTimeout)
        => phase switch
        {
            ArtisanPauseGatePhase.WaitingForAcknowledgement => EvaluateAcknowledgement(status, elapsed, acknowledgementTimeout),
            ArtisanPauseGatePhase.WaitingForIdle => EvaluateIdle(status, elapsed, idleTimeout),
            _ => new ArtisanPauseDecision(ArtisanPauseDecisionKind.Fail, $"未知的 Artisan 暂停阶段（{FormatStatus(status)}）"),
        };

    public static string FormatStatus(ArtisanPauseStatus status)
        => $"busy={status.IsBusy}, listRunning={status.IsListRunning}, listPaused={status.IsListPaused}, stopRequested={status.StopRequested}, endurance={status.EnduranceEnabled}";

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
                $"等待 Artisan 响应暂停请求超时（{FormatStatus(status)}）");

        return new ArtisanPauseDecision(ArtisanPauseDecisionKind.ContinueWaiting);
    }

    private static ArtisanPauseDecision EvaluateIdle(
        ArtisanPauseStatus status,
        TimeSpan elapsed,
        TimeSpan idleTimeout)
    {
        if (elapsed > idleTimeout)
            return new ArtisanPauseDecision(
                ArtisanPauseDecisionKind.Fail,
                $"等待 Artisan 退出超时（{FormatStatus(status)}）");

        return new ArtisanPauseDecision(ArtisanPauseDecisionKind.ContinueWaiting);
    }
}
