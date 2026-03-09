using System;

namespace Starloom.Automation;

public sealed class StateTimeoutTracker
{
    public DateTime EnteredAt { get; private set; } = DateTime.UtcNow;

    public void MarkEntered()
        => EnteredAt = DateTime.UtcNow;

    public bool TimedOut(TimeSpan timeout)
        => (DateTime.UtcNow - EnteredAt) > timeout;
}
