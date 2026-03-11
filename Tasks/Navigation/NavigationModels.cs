using System.Numerics;

namespace StarLoom.Tasks.Navigation;

public readonly record struct NavigationRequest(
    Vector3 destination,
    uint territoryId,
    string reason,
    bool useLifestream = false,
    string lifestreamCommand = "",
    bool allowFlight = false);
