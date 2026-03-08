using Starloom.IPC;
using Starloom.Services;

namespace Starloom.Core;

public sealed class JobContext
{
    public ArtisanIPC Artisan { get; init; } = null!;
    public NavigationService Navigation { get; init; } = null!;
    public NpcInteractionService NpcInteraction { get; init; } = null!;
    public Configuration Config { get; init; } = null!;
}
