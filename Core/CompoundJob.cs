using System.Collections.Generic;

namespace StarLoom.Core;

public sealed class CompoundJob : IAutomationJob
{
    private readonly List<IAutomationJob> _jobs;
    private int _currentIndex;
    private JobContext? _context;

    public string Id { get; }
    public string StatusText => _currentIndex < _jobs.Count ? _jobs[_currentIndex].StatusText : "已完成";
    public JobStatus Status { get; private set; } = JobStatus.Idle;

    public CompoundJob(string id, params IAutomationJob[] jobs)
    {
        Id = id;
        _jobs = new List<IAutomationJob>(jobs);
    }

    public bool CanStart() => _jobs.Count > 0 && _jobs[0].CanStart();

    public void Start(JobContext context)
    {
        _context = context;
        _currentIndex = 0;
        Status = JobStatus.Running;

        if (_jobs.Count > 0)
            _jobs[0].Start(context);
    }

    public void Update()
    {
        if (Status != JobStatus.Running || _currentIndex >= _jobs.Count)
            return;

        var current = _jobs[_currentIndex];
        current.Update();

        switch (current.Status)
        {
            case JobStatus.Completed:
                _currentIndex++;
                if (_currentIndex < _jobs.Count)
                {
                    if (_jobs[_currentIndex].CanStart())
                        _jobs[_currentIndex].Start(_context!);
                    else
                        Status = JobStatus.Completed;
                }
                else
                {
                    Status = JobStatus.Completed;
                }
                break;

            case JobStatus.Failed:
                Status = JobStatus.Failed;
                break;
        }
    }

    public void Stop()
    {
        if (_currentIndex < _jobs.Count)
            _jobs[_currentIndex].Stop();

        Status = JobStatus.Idle;
    }
}
