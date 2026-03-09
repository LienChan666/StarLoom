using System;
using System.Collections.Generic;

namespace Starloom.Core;

public sealed class StateMachine<TState> where TState : struct, Enum
{
    private readonly Dictionary<TState, Action> _handlers = new();
    private readonly Action<TState>? _transitionObserver;
    private readonly StateTimeoutTracker _timeoutTracker = new();

    public StateMachine(TState initialState, Action<TState>? transitionObserver = null)
    {
        State = initialState;
        _transitionObserver = transitionObserver;
        _timeoutTracker.MarkEntered();
    }

    public TState State { get; private set; }
    public DateTime StateEnteredAt => _timeoutTracker.EnteredAt;

    public void Configure(TState state, Action handler)
        => _handlers[state] = handler;

    public void TransitionTo(TState state)
    {
        State = state;
        _timeoutTracker.MarkEntered();
        _transitionObserver?.Invoke(state);
    }

    public bool TimedOut(TimeSpan timeout)
        => _timeoutTracker.TimedOut(timeout);

    public void Update()
    {
        if (_handlers.TryGetValue(State, out var handler))
            handler();
    }
}
