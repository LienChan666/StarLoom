namespace StarLoom.Tasks.Navigation;

public static class NavigationPlan
{
    public static bool ShouldUseLifestream(NavigationRequest navigationRequest)
    {
        return navigationRequest.useLifestream && !string.IsNullOrWhiteSpace(navigationRequest.lifestreamCommand);
    }
}
