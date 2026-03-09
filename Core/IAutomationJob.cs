namespace StarLoom.Core;

public interface IAutomationJob
{
    string Id { get; }
    JobStatus Status { get; }
    bool CanStart();
    void Start(JobContext context);
    void Update();
    void Stop();
}
