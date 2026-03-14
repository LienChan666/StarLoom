using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Linq;
using System.Numerics;

namespace Starloom.Services;

public sealed unsafe class NpcInteractionService
{
    private DateTime lastInteraction = DateTime.MinValue;
    private readonly TimeSpan interactionCooldown = TimeSpan.FromSeconds(1);

    public bool TryInteract(uint npcBaseId, float maxDistance = 6f)
    {
        if ((DateTime.UtcNow - lastInteraction) < interactionCooldown)
            return false;

        var npc = Svc.Objects.FirstOrDefault(x => x.BaseId == npcBaseId);
        if (npc == null)
            return false;

        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (Vector3.Distance(npc.Position, playerPos) > maxDistance)
            return false;

        Svc.Targets.Target = npc;
        TargetSystem.Instance()->OpenObjectInteraction((GameObject*)npc.Address);
        lastInteraction = DateTime.UtcNow;
        return true;
    }

    public bool TryInteract(IGameObject gameObject, float maxDistance = 6f)
    {
        if ((DateTime.UtcNow - lastInteraction) < interactionCooldown)
            return false;

        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (Vector3.Distance(gameObject.Position, playerPos) > maxDistance)
            return false;

        Svc.Targets.Target = gameObject;
        TargetSystem.Instance()->OpenObjectInteraction((GameObject*)gameObject.Address);
        lastInteraction = DateTime.UtcNow;
        return true;
    }
}
