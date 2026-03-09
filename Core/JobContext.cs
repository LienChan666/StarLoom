using StarLoom.IPC;
using StarLoom.Services.Interfaces;

namespace StarLoom.Core;

public sealed class JobContext
{
    public IArtisanIpc Artisan { get; init; } = null!;
    public INavigationService Navigation { get; init; } = null!;
    public INpcInteractionService NpcInteraction { get; init; } = null!;
    public IInventoryService Inventory { get; init; } = null!;
    public Configuration Config { get; init; } = null!;
}
