using Dalamud.Bindings.ImGui;
using Starloom.Data;
using Starloom.Services;
using Starloom.UI.Components.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Starloom.UI.Components.Settings;

internal sealed class CraftPointSettingsCard
{
    private List<HousingReturnPoint> cachedReturnPoints = [HousingReturnPoint.CreateInn()];
    private bool returnPointsCacheLoaded;
    private string returnPointsCacheStatusKey = "settings.craft_point.list_status_default";
    private object[] returnPointsCacheStatusArgs = [];
    private DateTime returnPointsCacheUpdatedAt = DateTime.MinValue;

    public void Draw()
    {
        var availablePoints = cachedReturnPoints;
        var configuredPoint = C.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        var resolvedPoint = availablePoints.FirstOrDefault(point => IsSamePoint(point, configuredPoint));
        var hasValidConfiguredPoint = resolvedPoint != null;
        var preview = hasValidConfiguredPoint
            ? GetPointLabel(resolvedPoint!)
            : configuredPoint.IsInn
                ? P.Localization.Get("settings.craft_point.inn")
                : configuredPoint.DisplayName is { Length: > 0 } savedName
                    ? savedName
                    : P.Localization.Get("settings.craft_point.inn");

        if (!GamePanelStyle.BeginSettingsTable("##CraftPointSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(P.Localization.Get("settings.craft_point.list"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button($"{P.Localization.Get("settings.craft_point.refresh_button")}##RefreshReturnPoints", new Vector2(140f, 0f)))
            RefreshReturnPointsCache();

        if (returnPointsCacheUpdatedAt != DateTime.MinValue)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(returnPointsCacheUpdatedAt.ToLocalTime().ToString("HH:mm:ss"));
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(P.Localization.Get("settings.craft_point.list_status"));
        ImGui.TableSetColumnIndex(1);
        ImGui.TextWrapped(P.Localization.Format(returnPointsCacheStatusKey, returnPointsCacheStatusArgs));

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(P.Localization.Get("settings.craft_point.default_point"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(320f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##DefaultCraftReturnPoint", preview))
        {
            foreach (var point in availablePoints)
            {
                var isSelected = IsSamePoint(point, configuredPoint);
                var pointLabel = GetPointLabel(point);
                if (ImGui.Selectable($"{pointLabel}##ReturnPoint_{point.AetheryteId}_{point.SubIndex}_{point.IsInn}", isSelected))
                {
                    C.DefaultCraftReturnPoint = ClonePoint(point);
                    P.ConfigStore.Save();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(P.Localization.Get("settings.craft_point.current_status"));
        ImGui.TableSetColumnIndex(1);
        if (configuredPoint.IsInn)
        {
            ImGui.TextWrapped(P.Localization.Get("settings.craft_point.current_inn"));
        }
        else if (!returnPointsCacheLoaded)
        {
            ImGui.TextWrapped(P.Localization.Format("settings.craft_point.current_saved_unverified", configuredPoint.DisplayName));
        }
        else if (!hasValidConfiguredPoint)
        {
            ImGui.TextColored(GamePanelStyle.Danger, P.Localization.Get("settings.craft_point.current_invalid"));
        }
        else
        {
            ImGui.TextWrapped(P.Localization.Format("settings.craft_point.current_configured", GetPointLabel(resolvedPoint!)));
        }

        ImGui.EndTable();
    }

    private void RefreshReturnPointsCache()
    {
        cachedReturnPoints = HousingReturnPointService.GetAvailableReturnPoints();
        returnPointsCacheLoaded = true;
        returnPointsCacheUpdatedAt = DateTime.UtcNow;

        if (cachedReturnPoints.Count > 1)
        {
            returnPointsCacheStatusKey = "settings.craft_point.list_status_loaded";
            returnPointsCacheStatusArgs = [cachedReturnPoints.Count];
            return;
        }

        returnPointsCacheStatusKey = "settings.craft_point.list_status_inn_only";
        returnPointsCacheStatusArgs = [];
    }

    private static string GetPointLabel(HousingReturnPoint point)
        => point.IsInn ? P.Localization.Get("settings.craft_point.inn") : point.DisplayName;

    private static bool IsSamePoint(HousingReturnPoint left, HousingReturnPoint right)
        => left.IsInn == right.IsInn
            && left.AetheryteId == right.AetheryteId
            && left.SubIndex == right.SubIndex;

    private static HousingReturnPoint ClonePoint(HousingReturnPoint point)
        => new()
        {
            AetheryteId = point.AetheryteId,
            SubIndex = point.SubIndex,
            TerritoryId = point.TerritoryId,
            IsInn = point.IsInn,
            IsApartment = point.IsApartment,
            DisplayName = point.DisplayName,
        };
}
