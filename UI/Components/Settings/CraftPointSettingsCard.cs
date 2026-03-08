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
    private readonly Plugin _plugin;
    private List<HousingReturnPoint> _cachedReturnPoints = [HousingReturnPoint.CreateInn()];
    private bool _returnPointsCacheLoaded;
    private string _returnPointsCacheStatus = "返回点列表始终包含旅馆；刷新后可加载住宅/公寓返回点。";
    private DateTime _returnPointsCacheUpdatedAt = DateTime.MinValue;

    public CraftPointSettingsCard(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        var availablePoints = _cachedReturnPoints;
        var configuredPoint = _plugin.Config.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        var resolvedPoint = availablePoints.FirstOrDefault(point => IsSamePoint(point, configuredPoint));
        var hasValidConfiguredPoint = resolvedPoint != null;
        var preview = hasValidConfiguredPoint
            ? resolvedPoint!.DisplayName
            : configuredPoint.DisplayName is { Length: > 0 } savedName
                ? savedName
                : "旅馆";

        if (!GamePanelStyle.BeginSettingsTable("##CraftPointSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("返回点列表");
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("刷新返回点列表", new Vector2(140f, 0f)))
            RefreshReturnPointsCache();

        if (_returnPointsCacheUpdatedAt != DateTime.MinValue)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_returnPointsCacheUpdatedAt.ToLocalTime().ToString("HH:mm:ss"));
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("列表状态");
        ImGui.TableSetColumnIndex(1);
        ImGui.TextWrapped(_returnPointsCacheStatus);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("默认返回点");
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(320f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##DefaultCraftReturnPoint", preview))
        {
            foreach (var point in availablePoints)
            {
                var isSelected = IsSamePoint(point, configuredPoint);
                if (ImGui.Selectable(point.DisplayName, isSelected))
                {
                    _plugin.Config.DefaultCraftReturnPoint = ClonePoint(point);
                    _plugin.Config.Save();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("当前状态");
        ImGui.TableSetColumnIndex(1);
        if (configuredPoint.IsInn)
        {
            ImGui.TextWrapped("当前默认返回点为旅馆。未手动修改时，初始化默认也是旅馆。");
        }
        else if (!_returnPointsCacheLoaded)
        {
            ImGui.TextWrapped($"已保存配置：{configuredPoint.DisplayName}（尚未验证，刷新后可重新校验并显示旅馆/住宅列表）");
        }
        else if (!hasValidConfiguredPoint)
        {
            ImGui.TextColored(GamePanelStyle.Danger, "当前返回点已失效，请重新选择；如不确定可直接改为旅馆。");
        }
        else
        {
            ImGui.TextWrapped($"已配置：{resolvedPoint!.DisplayName}");
        }

        ImGui.EndTable();
    }

    private void RefreshReturnPointsCache()
    {
        _cachedReturnPoints = HousingReturnPointService.GetAvailableReturnPoints();
        _returnPointsCacheLoaded = true;
        _returnPointsCacheUpdatedAt = DateTime.UtcNow;
        _returnPointsCacheStatus = _cachedReturnPoints.Count > 1
            ? $"已加载 {_cachedReturnPoints.Count} 个返回点（含旅馆）。"
            : "当前仅检测到旅馆返回点；若你有住宅/公寓，请在非过图状态下稍后重试。";
    }

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
