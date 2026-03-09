using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.UI;
using StarLoom.UI.Components.Shared;
using System;

namespace StarLoom.UI.Components.Settings;

internal sealed class PurchaseSettingsCard
{
    private readonly IPluginUiFacade _ui;

    public PurchaseSettingsCard(IPluginUiFacade ui)
    {
        _ui = ui;
    }

    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##PurchaseSettingsTable"))
            return;

        var buyAfterEachTurnIn = _ui.Config.BuyAfterEachTurnIn;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.purchase.auto_buy"));
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Checkbox($"{_ui.GetText("settings.purchase.auto_buy_toggle")}##BuyAfterEachTurnIn", ref buyAfterEachTurnIn))
        {
            _ui.Config.BuyAfterEachTurnIn = buyAfterEachTurnIn;
            _ui.SaveConfig();
        }

        var postPurchaseAction = _ui.Config.PostPurchaseAction;
        var actionPreview = _ui.GetText(postPurchaseAction == PurchaseCompletionAction.CloseGame
            ? "settings.purchase.action.close_game"
            : "settings.purchase.action.return_point");

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.purchase.action"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(220f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##PostPurchaseAction", actionPreview))
        {
            if (ImGui.Selectable($"{_ui.GetText("settings.purchase.action.return_point")}##PostPurchaseReturnPoint", postPurchaseAction == PurchaseCompletionAction.ReturnToConfiguredPoint))
            {
                _ui.Config.PostPurchaseAction = PurchaseCompletionAction.ReturnToConfiguredPoint;
                _ui.SaveConfig();
            }

            if (ImGui.Selectable($"{_ui.GetText("settings.purchase.action.close_game")}##PostPurchaseCloseGame", postPurchaseAction == PurchaseCompletionAction.CloseGame))
            {
                _ui.Config.PostPurchaseAction = PurchaseCompletionAction.CloseGame;
                _ui.SaveConfig();
            }

            ImGui.EndCombo();
        }

        var reserveScripAmount = _ui.Config.ReserveScripAmount;
        var previousReserveScripAmount = reserveScripAmount;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.purchase.reserve"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##ReserveScripAmount", ref reserveScripAmount, 0, 0))
            _ui.Config.ReserveScripAmount = Math.Max(0, reserveScripAmount);

        if (ImGui.IsItemDeactivatedAfterEdit() && _ui.Config.ReserveScripAmount != previousReserveScripAmount)
            _ui.SaveConfig();

        var freeSlotThreshold = _ui.Config.FreeSlotThreshold;
        var previousFreeSlotThreshold = freeSlotThreshold;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        GamePanelStyle.DrawSettingLabel(_ui.GetText("settings.purchase.free_slots"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##FreeSlotThreshold", ref freeSlotThreshold, 0, 0))
            _ui.Config.FreeSlotThreshold = Math.Max(0, freeSlotThreshold);

        if (ImGui.IsItemDeactivatedAfterEdit() && _ui.Config.FreeSlotThreshold != previousFreeSlotThreshold)
            _ui.SaveConfig();

        ImGui.EndTable();
    }
}

