using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.UI.Components.Shared;
using System;

namespace StarLoom.UI.Components.Settings;

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
        GamePanelStyle.DrawSettingLabel(_plugin.GetText("settings.purchase.auto_buy"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox($"{_plugin.GetText("settings.purchase.auto_buy_toggle")}##BuyAfterEachTurnIn", ref buyAfterEachTurnIn))
        {
            _plugin.Config.BuyAfterEachTurnIn = buyAfterEachTurnIn;
            _plugin.SaveConfig();
        }

        var postPurchaseAction = _plugin.Config.PostPurchaseAction;
        var actionPreview = _plugin.GetText(postPurchaseAction == PurchaseCompletionAction.CloseGame
            ? "settings.purchase.action.close_game"
            : "settings.purchase.action.return_point");

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_plugin.GetText("settings.purchase.action"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(220f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##PostPurchaseAction", actionPreview))
        {
            if (ImGui.Selectable($"{_plugin.GetText("settings.purchase.action.return_point")}##PostPurchaseReturnPoint", postPurchaseAction == PurchaseCompletionAction.ReturnToConfiguredPoint))
            {
                _plugin.Config.PostPurchaseAction = PurchaseCompletionAction.ReturnToConfiguredPoint;
                _plugin.SaveConfig();
            }

            if (ImGui.Selectable($"{_plugin.GetText("settings.purchase.action.close_game")}##PostPurchaseCloseGame", postPurchaseAction == PurchaseCompletionAction.CloseGame))
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
        GamePanelStyle.DrawSettingLabel(_plugin.GetText("settings.purchase.reserve"));
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
        GamePanelStyle.DrawSettingLabel(_plugin.GetText("settings.purchase.free_slots"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##FreeSlotThreshold", ref freeSlotThreshold, 0, 0))
            _plugin.Config.FreeSlotThreshold = Math.Max(0, freeSlotThreshold);

        if (ImGui.IsItemDeactivatedAfterEdit() && _plugin.Config.FreeSlotThreshold != previousFreeSlotThreshold)
            _plugin.SaveConfig();

        ImGui.EndTable();
    }
}
