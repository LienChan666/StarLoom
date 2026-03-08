using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Starloom.Data;
using Starloom.IPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Starloom.Services;

public static unsafe class HousingReturnPointService
{
    private const uint HouseEntranceDataId = 2002737;
    private const uint ApartmentEntranceDataId = 2007402;
    private static readonly HashSet<uint> ResidentialTerritories = [339, 340, 341, 641, 979];

    public static List<HousingReturnPoint> GetAvailableReturnPoints()
    {
        var result = new List<HousingReturnPoint>();
        result.Add(HousingReturnPoint.CreateInn());

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
            if (info.AetheryteId == 0)
                continue;
            if (!ResidentialTerritories.Contains(info.TerritoryId))
                continue;

            var key = ((ulong)info.AetheryteId << 8) | info.SubIndex;
            if (!seen.Add(key))
                continue;

            result.Add(new HousingReturnPoint
            {
                AetheryteId = info.AetheryteId,
                SubIndex = info.SubIndex,
                TerritoryId = info.TerritoryId,
                IsInn = false,
                IsApartment = info.IsApartment,
                DisplayName = BuildDisplayName(info.AetheryteId, info.IsApartment),
            });
        }

        return result
            .OrderBy(point => point.IsInn ? 0 : 1)
            .ThenBy(point => point.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    public static bool TryResolveConfiguredPoint(HousingReturnPoint configuredPoint, out HousingReturnPoint resolvedPoint)
    {
        if (configuredPoint.IsInn)
        {
            resolvedPoint = HousingReturnPoint.CreateInn();
            return true;
        }

        var resolved = GetAvailableReturnPoints().FirstOrDefault(point =>
            point.AetheryteId == configuredPoint.AetheryteId &&
            point.SubIndex == configuredPoint.SubIndex);

        if (resolved == null)
        {
            resolvedPoint = new HousingReturnPoint();
            return false;
        }

        resolvedPoint = resolved;
        return true;
    }

    public static bool TeleportTo(HousingReturnPoint point)
    {
        if (point.IsInn)
        {
            if (!LifestreamIPC.IsAvailable())
                return false;

            LifestreamIPC.EnqueueInnShortcut();
            return true;
        }

        return NativeTeleporter.Teleport(point.AetheryteId, point.SubIndex);
    }

    public static bool IsInsideHouse()
    {
        var housingManager = HousingManager.Instance();
        return housingManager != null && housingManager->IsInside();
    }

    public static bool IsInsideInn()
        => Inns.List.Contains(Svc.ClientState.TerritoryType);

    public static bool TryGetHousingEntrance(Vector3 origin, bool isApartment, out IGameObject? entrance)
    {
        var targetDataId = isApartment ? ApartmentEntranceDataId : HouseEntranceDataId;
        entrance = Svc.Objects
            .Where(obj => obj.ObjectKind == ObjectKind.EventObj && obj.BaseId == targetDataId)
            .OrderBy(obj => Vector3.DistanceSquared(obj.Position, origin))
            .FirstOrDefault();

        return entrance != null;
    }

    private static string BuildDisplayName(uint aetheryteId, bool isApartment)
    {
        var aetheryte = Svc.Data.GetExcelSheet<Aetheryte>()?.GetRow(aetheryteId);
        var territoryName = aetheryte?.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ToString() ?? "???";
        if (isApartment)
            return $"{territoryName} 公寓";

        var placeName = aetheryte?.PlaceName.ValueNullable?.Name.ToString() ?? "???";
        return $"{territoryName} {placeName}";
    }
}
