using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Linq;
using System.Numerics;

namespace Starloom.Services;

public sealed unsafe class NpcInteractionService
{
    private DateTime _lastInteraction = DateTime.MinValue;
    private readonly TimeSpan _interactionCooldown = TimeSpan.FromSeconds(1);

    public bool TryInteract(uint npcBaseId, float maxDistance = 6f)
    {
        if ((DateTime.UtcNow - _lastInteraction) < _interactionCooldown)
            return false;

        var npc = Svc.Objects.FirstOrDefault(x => x.BaseId == npcBaseId);
        if (npc == null)
            return false;

        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (Vector3.Distance(npc.Position, playerPos) > maxDistance)
            return false;

        Svc.Targets.Target = npc;
        TargetSystem.Instance()->OpenObjectInteraction((GameObject*)npc.Address);
        _lastInteraction = DateTime.UtcNow;
        return true;
    }

    public bool TryInteract(IGameObject gameObject, float maxDistance = 6f)
    {
        if ((DateTime.UtcNow - _lastInteraction) < _interactionCooldown)
            return false;

        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (Vector3.Distance(gameObject.Position, playerPos) > maxDistance)
            return false;

        Svc.Targets.Target = gameObject;
        TargetSystem.Instance()->OpenObjectInteraction((GameObject*)gameObject.Address);
        _lastInteraction = DateTime.UtcNow;
        return true;
    }
}
