using Dalamud.Bindings.ImGui;
using Starloom.Data;
using Starloom.UI.Components.Shared;
using System;

namespace Starloom.UI.Components.Settings;

internal sealed class PurchaseSettingsCard
{
    private readonly Plugin _plugin;

    public PurchaseSettingsCard(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##PurchaseSettingsTable"))
            return;

        var buyAfterEachTurnIn = _plugin.Config.BuyAfterEachTurnIn;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("自动购买");
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox("提交后自动购买", ref buyAfterEachTurnIn))
        {
            _plugin.Config.BuyAfterEachTurnIn = buyAfterEachTurnIn;
            _plugin.SaveConfig();
        }

        var postPurchaseAction = _plugin.Config.PostPurchaseAction;
        var actionPreview = postPurchaseAction == PurchaseCompletionAction.CloseGame
            ? "关闭游戏"
            : "返回配置的返回点";
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("完成动作");
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(220f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##PostPurchaseAction", actionPreview))
        {
            if (ImGui.Selectable("返回配置的返回点", postPurchaseAction == PurchaseCompletionAction.ReturnToConfiguredPoint))
            {
                _plugin.Config.PostPurchaseAction = PurchaseCompletionAction.ReturnToConfiguredPoint;
                _plugin.SaveConfig();
            }

            if (ImGui.Selectable("关闭游戏", postPurchaseAction == PurchaseCompletionAction.CloseGame))
            {
                _plugin.Config.PostPurchaseAction = PurchaseCompletionAction.CloseGame;
                _plugin.SaveConfig();
            }

            ImGui.EndCombo();
        }

        var reserveScripAmount = _plugin.Config.ReserveScripAmount;
        var previousReserveScripAmount = reserveScripAmount;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("工票预留");
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##ReserveScripAmount", ref reserveScripAmount, 0, 0))
            _plugin.Config.ReserveScripAmount = Math.Max(0, reserveScripAmount);

        if (ImGui.IsItemDeactivatedAfterEdit() && _plugin.Config.ReserveScripAmount != previousReserveScripAmount)
            _plugin.SaveConfig();

        var freeSlotThreshold = _plugin.Config.FreeSlotThreshold;
        var previousFreeSlotThreshold = freeSlotThreshold;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel("背包保护");
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##FreeSlotThreshold", ref freeSlotThreshold, 0, 0))
            _plugin.Config.FreeSlotThreshold = Math.Max(0, freeSlotThreshold);

        if (ImGui.IsItemDeactivatedAfterEdit() && _plugin.Config.FreeSlotThreshold != previousFreeSlotThreshold)
            _plugin.SaveConfig();

        ImGui.EndTable();
    }
}
