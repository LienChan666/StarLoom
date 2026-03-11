using Dalamud.Bindings.ImGui;
using Starloom.Data;
using Starloom.UI.Components.Shared;
using System;

namespace Starloom.UI.Components.Settings;

internal sealed class PurchaseSettingsCard
{
    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##PurchaseSettingsTable"))
            return;

        var postPurchaseAction = C.PostPurchaseAction;
        var actionPreview = P.Localization.Get(postPurchaseAction == PurchaseCompletionAction.CloseGame
            ? "settings.purchase.action.close_game"
            : "settings.purchase.action.return_point");

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(P.Localization.Get("settings.purchase.action"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(220f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##PostPurchaseAction", actionPreview))
        {
            if (ImGui.Selectable($"{P.Localization.Get("settings.purchase.action.return_point")}##PostPurchaseReturnPoint", postPurchaseAction == PurchaseCompletionAction.ReturnToConfiguredPoint))
            {
                C.PostPurchaseAction = PurchaseCompletionAction.ReturnToConfiguredPoint;
                P.ConfigStore.Save();
            }

            if (ImGui.Selectable($"{P.Localization.Get("settings.purchase.action.close_game")}##PostPurchaseCloseGame", postPurchaseAction == PurchaseCompletionAction.CloseGame))
            {
                C.PostPurchaseAction = PurchaseCompletionAction.CloseGame;
                P.ConfigStore.Save();
            }

            ImGui.EndCombo();
        }

        var reserveScripAmount = C.ReserveScripAmount;
        var previousReserveScripAmount = reserveScripAmount;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(P.Localization.Get("settings.purchase.reserve"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##ReserveScripAmount", ref reserveScripAmount, 0, 0))
            C.ReserveScripAmount = Math.Max(0, reserveScripAmount);

        if (ImGui.IsItemDeactivatedAfterEdit() && C.ReserveScripAmount != previousReserveScripAmount)
            P.ConfigStore.Save();

        var freeSlotThreshold = C.FreeSlotThreshold;
        var previousFreeSlotThreshold = freeSlotThreshold;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(P.Localization.Get("settings.purchase.free_slots"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##FreeSlotThreshold", ref freeSlotThreshold, 0, 0))
            C.FreeSlotThreshold = Math.Max(0, freeSlotThreshold);

        if (ImGui.IsItemDeactivatedAfterEdit() && C.FreeSlotThreshold != previousFreeSlotThreshold)
            P.ConfigStore.Save();

        ImGui.EndTable();
    }
}
