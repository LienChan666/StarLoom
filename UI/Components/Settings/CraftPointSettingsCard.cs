using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.Services;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace StarLoom.UI.Components.Settings;

internal sealed class CraftPointSettingsCard
{
    private readonly IPluginUiFacade _ui;
    private List<HousingReturnPoint> _cachedReturnPoints = [HousingReturnPoint.CreateInn()];
    private bool _returnPointsCacheLoaded;
    private string _returnPointsCacheStatusKey = "settings.craft_point.list_status_default";
    private object[] _returnPointsCacheStatusArgs = [];
    private DateTime _returnPointsCacheUpdatedAt = DateTime.MinValue;

    public CraftPointSettingsCard(IPluginUiFacade ui)
    {
        _ui = ui;
    }

    public void Draw()
    {
        var availablePoints = _cachedReturnPoints;
        var configuredPoint = _ui.Config.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        var resolvedPoint = availablePoints.FirstOrDefault(point => IsSamePoint(point, configuredPoint));
        var hasValidConfiguredPoint = resolvedPoint != null;
        var preview = hasValidConfiguredPoint
            ? GetPointLabel(resolvedPoint!)
            : configuredPoint.IsInn
                ? _ui.GetText("settings.craft_point.inn")
                : configuredPoint.DisplayName is { Length: > 0 } savedName
                    ? savedName
                    : _ui.GetText("settings.craft_point.inn");

        if (!GamePanelStyle.BeginSettingsTable("##CraftPointSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.craft_point.list"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button($"{_ui.GetText("settings.craft_point.refresh_button")}##RefreshReturnPoints", new Vector2(140f, 0f)))
            RefreshReturnPointsCache();

        if (_returnPointsCacheUpdatedAt != DateTime.MinValue)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_returnPointsCacheUpdatedAt.ToLocalTime().ToString("HH:mm:ss"));
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.craft_point.list_status"));
        ImGui.TableSetColumnIndex(1);
        ImGui.TextWrapped(_ui.GetText(_returnPointsCacheStatusKey, _returnPointsCacheStatusArgs));

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.craft_point.default_point"));
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
                    _ui.Config.DefaultCraftReturnPoint = ClonePoint(point);
                    _ui.SaveConfig();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.craft_point.current_status"));
        ImGui.TableSetColumnIndex(1);
        if (configuredPoint.IsInn)
        {
            ImGui.TextWrapped(_ui.GetText("settings.craft_point.current_inn"));
        }
        else if (!_returnPointsCacheLoaded)
        {
            ImGui.TextWrapped(_ui.GetText("settings.craft_point.current_saved_unverified", configuredPoint.DisplayName));
        }
        else if (!hasValidConfiguredPoint)
        {
            ImGui.TextColored(GamePanelStyle.Danger, _ui.GetText("settings.craft_point.current_invalid"));
        }
        else
        {
            ImGui.TextWrapped(_ui.GetText("settings.craft_point.current_configured", GetPointLabel(resolvedPoint!)));
        }

        ImGui.EndTable();
    }

    private void RefreshReturnPointsCache()
    {
        _cachedReturnPoints = HousingReturnPointService.GetAvailableReturnPoints();
        _returnPointsCacheLoaded = true;
        _returnPointsCacheUpdatedAt = DateTime.UtcNow;

        if (_cachedReturnPoints.Count > 1)
        {
            _returnPointsCacheStatusKey = "settings.craft_point.list_status_loaded";
            _returnPointsCacheStatusArgs = [_cachedReturnPoints.Count];
            return;
        }

        _returnPointsCacheStatusKey = "settings.craft_point.list_status_inn_only";
        _returnPointsCacheStatusArgs = [];
    }

    private string GetPointLabel(HousingReturnPoint point)
        => point.IsInn ? _ui.GetText("settings.craft_point.inn") : point.DisplayName;

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

