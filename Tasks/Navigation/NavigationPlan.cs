using System.Numerics;

namespace StarLoom.Tasks.Navigation;

public static class NavigationPlan
{
    public static bool ShouldUseLifestream(NavigationRequest navigationRequest, float distanceToDestination)
    {
        return navigationRequest.useLifestream
            && !string.IsNullOrWhiteSpace(navigationRequest.lifestreamCommand)
            && distanceToDestination > 40f;
    }

    public static bool HasArrived(NavigationRequest navigationRequest, NavigationRuntimeSnapshot snapshot)
    {
        return snapshot.currentTerritoryId == navigationRequest.territoryId
            && GetDistanceToDestination(navigationRequest, snapshot) <= navigationRequest.arrivalDistance;
    }

    public static float GetDistanceToDestination(NavigationRequest navigationRequest, NavigationRuntimeSnapshot snapshot)
    {
        if (snapshot.playerPosition == default || snapshot.currentTerritoryId != navigationRequest.territoryId)
            return float.MaxValue;

        return Vector3.Distance(snapshot.playerPosition, navigationRequest.destination);
    }
}
