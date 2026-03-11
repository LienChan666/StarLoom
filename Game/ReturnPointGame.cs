using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.UIHelpers.AddonMasterImplementations;
using ECommons.ExcelServices.TerritoryEnumeration;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using StarLoom.Config;
using StarLoom.Ipc;
using System.Numerics;
using static ECommons.GenericHelpers;

namespace StarLoom.Game;

public static unsafe class ReturnPointGame
{
    private const uint HouseEntranceDataId = 2002737;
    private const uint ApartmentEntranceDataId = 2007402;
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
                isInn = false,
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

    public static bool TryResolveConfiguredPoint(ReturnPointConfig configuredPoint, out ReturnPointConfig resolvedPoint)
    {
        if (configuredPoint.isInn)
        {
            resolvedPoint = ReturnPointConfig.CreateInn();
            return true;
        }

        var resolved = GetAvailableReturnPoints().FirstOrDefault(point =>
            point.aetheryteId == configuredPoint.aetheryteId &&
            point.subIndex == configuredPoint.subIndex);

        if (resolved == null)
        {
            resolvedPoint = new ReturnPointConfig();
            return false;
        }

        resolvedPoint = resolved;
        return true;
    }

    public static bool CanEnterDirectlyFromCurrentLocation(ReturnPointConfig point)
    {
        if (point.isInn)
            return false;

        if (Svc.ClientState.TerritoryType != point.territoryId)
            return false;

        if (Svc.Objects.LocalPlayer is not { } localPlayer)
            return false;

        return TryGetHousingEntrance(localPlayer.Position, point.isApartment, out var entrance)
            && entrance != null;
    }

    public static bool TryTeleportToReturnPoint(ReturnPointConfig point, ILifestreamIpc lifestreamIpc)
    {
        if (point.isInn)
        {
            if (!lifestreamIpc.IsAvailable())
                return false;

            lifestreamIpc.EnqueueInnShortcut();
            return true;
        }

        if (CanEnterDirectlyFromCurrentLocation(point))
            return true;

        return LocationGame.TeleportToAetheryte(point.aetheryteId, point.subIndex);
    }

    public static bool IsInsideHouse()
    {
        var housingManager = HousingManager.Instance();
        return housingManager != null && housingManager->IsInside();
    }

    public static bool IsInsideInn()
    {
        return Inns.List.Contains(Svc.ClientState.TerritoryType);
    }

    public static bool TryGetHousingEntrance(Vector3 origin, bool isApartment, out IGameObject? entrance)
    {
        var targetDataId = isApartment ? ApartmentEntranceDataId : HouseEntranceDataId;
        entrance = Svc.Objects
            .Where(obj => obj.ObjectKind == ObjectKind.EventObj && obj.BaseId == targetDataId)
            .OrderBy(obj => Vector3.DistanceSquared(obj.Position, origin))
            .FirstOrDefault();

        return entrance != null;
    }

    public static bool TryConfirmEntry(bool isApartment)
    {
        if (isApartment)
        {
            if (TryGetAddonByName<AddonSelectString>("SelectString", out var selectStringAddon)
                && IsAddonReady(&selectStringAddon->AtkUnitBase))
            {
                var selectString = new AddonMaster.SelectString((nint)selectStringAddon);
                if (selectString.EntryCount > 0)
                {
                    selectString.Entries[0].Select();
                    return true;
                }
            }

            return false;
        }

        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var yesnoAddon)
            && IsAddonReady(&yesnoAddon->AtkUnitBase))
        {
            new AddonMaster.SelectYesno((nint)yesnoAddon).Yes();
            return true;
        }

        return false;
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
