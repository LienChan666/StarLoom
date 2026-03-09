using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.UI.Components.Shared;
using System;
using System.Numerics;

namespace StarLoom.UI.Components.Settings;

internal sealed class CatalogSettingsCard
{
    private readonly Plugin _plugin;

    public CatalogSettingsCard(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##CatalogSettingsTable"))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("物品缓存");
        ImGui.TableSetColumnIndex(1);
        GamePanelStyle.DrawHint("找不到兑换物品时，可在这里主动刷新目录缓存。");
        ImGui.BeginDisabled(ScripShopItemManager.IsLoading);
        if (ImGui.Button("刷新物品列表", new Vector2(120f, 0f)))
            _plugin.ScripShopItemManager.RequestRefresh();
        ImGui.EndDisabled();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("当前状态");
        ImGui.TableSetColumnIndex(1);
        DrawCatalogStatusText();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("缓存文件");
        ImGui.TableSetColumnIndex(1);
        ImGui.TextWrapped(_plugin.ScripShopItemManager.CacheFilePath);

        ImGui.EndTable();
    }

    private void DrawCatalogStatusText()
    {
        var color = GetCatalogStatusColor(_plugin.ScripShopItemManager.StatusMessage);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(_plugin.ScripShopItemManager.StatusMessage);
        ImGui.PopStyleColor();
    }

    private static Vector4 GetCatalogStatusColor(string message)
    {
        if (ScripShopItemManager.IsLoading)
            return GamePanelStyle.Warning;

        if (message.Contains("失败", StringComparison.Ordinal))
            return GamePanelStyle.Danger;

        return GamePanelStyle.Success;
    }
}
