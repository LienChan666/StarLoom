using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Numerics;

namespace StarLoom.Game;

public sealed unsafe class NpcGame
{
    private DateTime lastInteraction = DateTime.MinValue;
    private readonly TimeSpan interactionCooldown = TimeSpan.FromSeconds(1);

    public bool TryInteract(uint npcId, float maxDistance = 6f)
    {
        if ((DateTime.UtcNow - lastInteraction) < interactionCooldown)
            return false;

        var npc = Svc.Objects.FirstOrDefault(obj => obj.BaseId == npcId);
        if (npc == null)
            return false;

        return TryInteract(npc, maxDistance);
    }

    public bool TryInteract(IGameObject gameObject, float maxDistance = 6f)
    {
        if ((DateTime.UtcNow - lastInteraction) < interactionCooldown)
            return false;

        var playerPosition = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (Vector3.Distance(gameObject.Position, playerPosition) > maxDistance)
            return false;

        Svc.Targets.Target = gameObject;
        TargetSystem.Instance()->OpenObjectInteraction((GameObject*)gameObject.Address);
        lastInteraction = DateTime.UtcNow;
        return true;
    }
}
