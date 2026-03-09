using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Starloom.Services;

public static unsafe class NativeTeleporter
{
    public static bool IsAttuned(uint aetheryteId)
    {
        var telepo = Telepo.Instance();
        if (telepo == null)
            return false;

        if (!Svc.PlayerState.IsLoaded)
            return true;

        telepo->UpdateAetheryteList();
        var endPtr = telepo->TeleportList.Last;
        for (var it = telepo->TeleportList.First; it != endPtr; ++it)
        {
            if (it->AetheryteId == aetheryteId)
                return true;
        }

        return false;
    }

    public static bool Teleport(uint aetheryteId)
        => Teleport(aetheryteId, 0);

    public static bool Teleport(uint aetheryteId, byte subIndex)
    {
        if (!IsAttuned(aetheryteId))
            return false;

        Telepo.Instance()->Teleport(aetheryteId, subIndex);
        return true;
    }
}
