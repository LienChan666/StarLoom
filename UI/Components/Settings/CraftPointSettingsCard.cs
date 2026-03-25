using Dalamud.Bindings.ImGui;
using Starloom.Data;
using Starloom.Services;
using Starloom.UI.Components.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Starloom.UI.Components.Settings;

internal sealed class CraftPointSettingsCard
{
    private List<HousingReturnPoint> cachedReturnPoints = [HousingReturnPoint.CreateInn()];
    private bool returnPointsCacheLoaded;
    private bool returnPointComboOpen;

    public void Draw()
    {
        var configuredPoint = C.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        var resolvedPoint = returnPointsCacheLoaded
            ? cachedReturnPoints.FirstOrDefault(point => IsSamePoint(point, configuredPoint))
            : null;
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
        ImGui.TextUnformatted(P.Localization.Get("settings.craft_point.list"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(320f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##DefaultCraftReturnPoint", preview))
        {
            if (!returnPointsCacheLoaded || !returnPointComboOpen)
            {
                RefreshReturnPointsCache();
                returnPointComboOpen = true;
            }

            foreach (var point in cachedReturnPoints)
            {
                var isSelected = IsSamePoint(point, configuredPoint);
                var pointLabel = GetPointLabel(point);
                if (ImGui.Selectable($"{pointLabel}##ReturnPoint_{point.AetheryteId}_{point.SubIndex}_{point.IsInn}", isSelected))
                    P.ConfigEditor.SetDefaultCraftReturnPoint(point);

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
        else
        {
            returnPointComboOpen = false;
        }

        ImGui.EndTable();
    }

    private void RefreshReturnPointsCache()
    {
        cachedReturnPoints = HousingReturnPointService.GetAvailableReturnPoints();
        returnPointsCacheLoaded = true;
    }

    private static string GetPointLabel(HousingReturnPoint point)
        => point.IsInn ? P.Localization.Get("settings.craft_point.inn") : point.DisplayName;

    private static bool IsSamePoint(HousingReturnPoint left, HousingReturnPoint right)
        => left.IsInn == right.IsInn
            && left.AetheryteId == right.AetheryteId
            && left.SubIndex == right.SubIndex;
}
