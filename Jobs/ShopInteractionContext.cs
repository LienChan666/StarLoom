using StarLoom.Services;

namespace StarLoom.Jobs;

public sealed record ShopInteractionContext(
    NavigationTarget Target,
    uint NpcId,
    string NavigationFailureMessage,
    string WindowTimeoutMessage);
