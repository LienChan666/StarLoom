using System;
using System.Collections.Generic;

namespace Starloom.Automation;

public sealed class StateMachine<TState> where TState : struct, Enum
{
    private readonly Dictionary<TState, Action> handlers = new();
    private readonly Action<TState>? transitionObserver;
    private readonly StateTimeoutTracker timeoutTracker = new();

    public StateMachine(TState initialState, Action<TState>? transitionObserver = null)
    {
        State = initialState;
        this.transitionObserver = transitionObserver;
        timeoutTracker.MarkEntered();
    }

    public TState State { get; private set; }
    public DateTime StateEnteredAt => timeoutTracker.EnteredAt;

    public void Configure(TState state, Action handler)
        => handlers[state] = handler;

    public void TransitionTo(TState state)
    {
        State = state;
        timeoutTracker.MarkEntered();
        transitionObserver?.Invoke(state);
    }

    public bool TimedOut(TimeSpan timeout)
        => timeoutTracker.TimedOut(timeout);

    public void Update()
    {
        if (handlers.TryGetValue(State, out var handler))
            handler();
    }
}
