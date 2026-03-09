namespace StarLoom.Services.Interfaces;

public enum NavigationStatus
{
    Idle,
    Teleporting,
    WaitingForTeleport,
    UsingLifestream,
    WaitingForLifestream,
    Pathfinding,
    WaitingForArrival,
    Arrived,
    Failed,
}
