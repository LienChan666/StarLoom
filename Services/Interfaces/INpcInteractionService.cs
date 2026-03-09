using Dalamud.Game.ClientState.Objects.Types;

namespace Starloom.Services.Interfaces;

public interface INpcInteractionService
{
    bool TryInteract(uint npcBaseId, float maxDistance = 6f);
    bool TryInteract(IGameObject gameObject, float maxDistance = 6f);
}
