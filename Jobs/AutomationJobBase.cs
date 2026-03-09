using ECommons.DalamudServices;
using StarLoom.Core;
using System;

namespace StarLoom.Jobs;

public abstract class AutomationJobBase : IAutomationJob
{
    protected JobContext? Context { get; private set; }
    protected DateTime LastActionAt { get; set; } = DateTime.MinValue;
    protected DateTime TransitionedAt { get; set; } = DateTime.MinValue;

    public abstract string Id { get; }
    public JobStatus Status { get; protected set; } = JobStatus.Idle;

    public abstract bool CanStart();

    public virtual void Start(JobContext context)
    {
        Context = context;
        Status = JobStatus.Running;
    }

    public abstract void Update();

    public virtual void Stop()
    {
        StopNavigation();
        Status = JobStatus.Idle;
    }

    protected void MarkCompleted()
    {
        StopNavigation();
        Status = JobStatus.Completed;
    }

    protected void FailJob(string message)
    {
        StopNavigation();
        Status = JobStatus.Failed;
        Svc.Log.Error($"[Starloom] {GetType().Name} failed: {message}");
    }

    protected void StopNavigation()
        => Context?.Navigation.Stop();
}
