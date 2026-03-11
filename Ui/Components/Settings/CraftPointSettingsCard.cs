using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Game;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;
using System.Numerics;

namespace StarLoom.Ui.Components.Settings;

internal sealed class CraftPointSettingsCard
{
    private List<ReturnPointConfig> cachedReturnPoints = [ReturnPointConfig.CreateInn()];
    private bool returnPointsCacheLoaded;
    private string returnPointsCacheStatusKey = "settings.craft_point.list_status_default";
    private object[] returnPointsCacheStatusArgs = [];
    private DateTime returnPointsCacheUpdatedAt = DateTime.MinValue;
    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public CraftPointSettingsCard(ConfigStore configStore, UiText uiText)
    {
        this.configStore = configStore;
        this.uiText = uiText;
    }

    public void Draw()
    {
        var availablePoints = cachedReturnPoints;
        var configuredPoint = configStore.pluginConfig.defaultReturnPoint ?? ReturnPointConfig.CreateInn();
        var resolvedPoint = availablePoints.FirstOrDefault(point => IsSamePoint(point, configuredPoint));
        var hasValidConfiguredPoint = resolvedPoint != null;
        var preview = hasValidConfiguredPoint
            ? GetPointLabel(resolvedPoint!)
            : configuredPoint.isInn
                ? uiText.Get("settings.craft_point.inn")
                : !string.IsNullOrWhiteSpace(configuredPoint.displayName)
                    ? configuredPoint.displayName
                    : uiText.Get("settings.craft_point.inn");

        if (!GamePanelStyle.BeginSettingsTable("##CraftPointSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.craft_point.list"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button($"{uiText.Get("settings.craft_point.refresh_button")}##RefreshReturnPoints", new Vector2(140f, 0f)))
            RefreshReturnPointsCache();

        if (returnPointsCacheUpdatedAt != DateTime.MinValue)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(returnPointsCacheUpdatedAt.ToLocalTime().ToString("HH:mm:ss"));
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.craft_point.list_status"));
        ImGui.TableSetColumnIndex(1);
        ImGui.TextWrapped(uiText.Format(returnPointsCacheStatusKey, returnPointsCacheStatusArgs));

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.craft_point.default_point"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(320f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##DefaultCraftReturnPoint", preview))
        {
            foreach (var point in availablePoints)
            {
                var isSelected = IsSamePoint(point, configuredPoint);
                var pointLabel = GetPointLabel(point);
                if (ImGui.Selectable($"{pointLabel}##ReturnPoint_{point.aetheryteId}_{point.subIndex}_{point.isInn}", isSelected))
                {
                    configStore.pluginConfig.defaultReturnPoint = ClonePoint(point);
                    configStore.Save();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.craft_point.current_status"));
        ImGui.TableSetColumnIndex(1);
        if (configuredPoint.isInn)
        {
            ImGui.TextWrapped(uiText.Get("settings.craft_point.current_inn"));
        }
        else if (!returnPointsCacheLoaded)
        {
            ImGui.TextWrapped(uiText.Format("settings.craft_point.current_saved_unverified", configuredPoint.displayName));
        }
        else if (!hasValidConfiguredPoint)
        {
            ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), uiText.Get("settings.craft_point.current_invalid"));
        }
        else
        {
            ImGui.TextWrapped(uiText.Format("settings.craft_point.current_configured", GetPointLabel(resolvedPoint!)));
        }

        ImGui.EndTable();
    }

    private void RefreshReturnPointsCache()
    {
        cachedReturnPoints = ReturnPointGame.GetAvailableReturnPoints();
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

    private string GetPointLabel(ReturnPointConfig point)
    {
        return point.isInn ? uiText.Get("settings.craft_point.inn") : point.displayName;
    }

    private static bool IsSamePoint(ReturnPointConfig left, ReturnPointConfig right)
    {
        return left.isInn == right.isInn
            && left.aetheryteId == right.aetheryteId
            && left.subIndex == right.subIndex;
    }

    private static ReturnPointConfig ClonePoint(ReturnPointConfig point)
    {
        return new ReturnPointConfig
        {
            isInn = point.isInn,
            territoryId = point.territoryId,
            aetheryteId = point.aetheryteId,
            subIndex = point.subIndex,
            isApartment = point.isApartment,
            displayName = point.displayName,
        };
    }
}
