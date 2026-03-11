using System.Numerics;

namespace StarLoom.Tasks.Navigation;

public enum NavigationStage
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

public readonly record struct NavigationRuntimeSnapshot(
    uint currentTerritoryId,
    Vector3 playerPosition,
    bool isLocalControlReady);

public readonly record struct NavigationRequest(
    Vector3 destination,
    uint territoryId,
    string reason,
    uint aetheryteId = 0,
    float arrivalDistance = 2f,
    bool useLifestream = false,
    string lifestreamCommand = "",
    bool allowFlight = false);
