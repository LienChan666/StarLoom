using StarLoom.Core;
using System.Diagnostics;

namespace StarLoom.Jobs;

public sealed class CloseGameJob : IAutomationJob
{
    public string Id => "close-game";
    public JobStatus Status { get; private set; } = JobStatus.Idle;

    public bool CanStart() => true;

    public void Start(JobContext context)
    {
        Status = JobStatus.Running;
    }

    public void Update()
    {
        if (Status != JobStatus.Running)
            return;

        Process.GetCurrentProcess().Kill();
    }

    public void Stop()
    {
        Status = JobStatus.Idle;
    }
}
