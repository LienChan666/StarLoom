namespace Starloom.Automation;

internal enum WorkflowState
{
    Idle,
    WaitingForStartReturn,
    MonitoringArtisan,
    LoopingTurnInAndPurchase,
    FinalizingCompletion,
    Failed,
}
