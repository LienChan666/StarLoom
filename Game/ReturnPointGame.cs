using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using StarLoom.Config;

namespace StarLoom.Game;

public static unsafe class ReturnPointGame
{
    private static readonly HashSet<uint> ResidentialTerritories = [339, 340, 341, 641, 979];

    public static List<ReturnPointConfig> GetAvailableReturnPoints()
    {
        var result = new List<ReturnPointConfig>
        {
            ReturnPointConfig.CreateInn(),
        };

        var telepo = Telepo.Instance();
        if (telepo == null || !Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer == null)
            return result;

        if (Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51])
            return result;

        if (telepo->UpdateAetheryteList() == null)
            return result;

        var count = telepo->TeleportList.Count;
        if (count <= 0 || count > 512)
            return result;

        var seen = new HashSet<ulong>();
        for (var index = 0; index < count; index++)
        {
            var info = telepo->TeleportList[index];
            if (info.AetheryteId == 0 || !ResidentialTerritories.Contains(info.TerritoryId))
                continue;

            var key = ((ulong)info.AetheryteId << 8) | info.SubIndex;
            if (!seen.Add(key))
                continue;

            result.Add(new ReturnPointConfig
            {
                kind = "housing",
                territoryId = info.TerritoryId,
                aetheryteId = info.AetheryteId,
                subIndex = info.SubIndex,
                isApartment = info.IsApartment,
                displayName = BuildDisplayName(info.AetheryteId, info.IsApartment),
            });
        }

        return result
            .OrderBy(point => point.isInn ? 0 : 1)
            .ThenBy(point => point.displayName, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildDisplayName(uint aetheryteId, bool isApartment)
    {
        var aetheryte = Svc.Data.GetExcelSheet<Aetheryte>()?.GetRow(aetheryteId);
        var territoryName = aetheryte?.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ToString() ?? "???";
        if (isApartment)
            return $"{territoryName} Apartment";

        var placeName = aetheryte?.PlaceName.ValueNullable?.Name.ToString() ?? "???";
        return $"{territoryName} {placeName}";
    }
}
