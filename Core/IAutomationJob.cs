namespace Starloom.Core;

public interface IAutomationJob
{
    string Id { get; }
    string StatusText { get; }
    JobStatus Status { get; }
    bool CanStart();
    void Start(JobContext context);
    void Update();
    void Stop();
}
