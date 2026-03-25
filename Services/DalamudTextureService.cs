using Dalamud.IoC;

namespace Starloom.Services;

internal sealed class DalamudTextureService
{
    [PluginService]
    internal ITextureProvider Textures { get; private set; } = null!;
}
