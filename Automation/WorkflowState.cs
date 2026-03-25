namespace Starloom.Automation;

internal enum WorkflowState
{
    Idle,
    WaitingForStartReturn,
    StartingArtisan,
    Running,
    ReturningToCraftPoint,
    Failed,
}
