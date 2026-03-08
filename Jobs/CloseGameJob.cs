using Starloom.Core;
using System.Diagnostics;

namespace Starloom.Jobs;

public sealed class CloseGameJob : IAutomationJob
{
    public string Id => "close-game";
    public string StatusText { get; private set; } = "空闲";
    public JobStatus Status { get; private set; } = JobStatus.Idle;

    public bool CanStart() => true;

    public void Start(JobContext context)
    {
        Status = JobStatus.Running;
        StatusText = "正在关闭游戏";
    }

    public void Update()
    {
        if (Status != JobStatus.Running)
            return;

        StatusText = "正在关闭游戏";
        Process.GetCurrentProcess().Kill();
    }

    public void Stop()
    {
        Status = JobStatus.Idle;
        StatusText = "已停止";
    }
}
