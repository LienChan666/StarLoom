using Dalamud.Bindings.ImGui;
using StarLoom.Config;
using StarLoom.Ui;
using StarLoom.Ui.Components.Shared;

namespace StarLoom.Ui.Components.Settings;

internal sealed class PurchaseSettingsCard
{
    private readonly ConfigStore configStore;
    private readonly UiText uiText;

    public PurchaseSettingsCard(ConfigStore configStore, UiText uiText)
    {
        this.configStore = configStore;
        this.uiText = uiText;
    }

    public void Draw()
    {
        if (!GamePanelStyle.BeginSettingsTable("##PurchaseSettingsTable"))
            return;

        var postPurchaseAction = configStore.pluginConfig.postPurchaseAction;
        var actionPreview = uiText.Get(postPurchaseAction == PostPurchaseAction.CloseGame
            ? "settings.purchase.action.close_game"
            : "settings.purchase.action.return_point");

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.purchase.action"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(220f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##PostPurchaseAction", actionPreview))
        {
            if (ImGui.Selectable($"{uiText.Get("settings.purchase.action.return_point")}##PostPurchaseReturnPoint", postPurchaseAction == PostPurchaseAction.ReturnToConfiguredPoint))
            {
                configStore.pluginConfig.postPurchaseAction = PostPurchaseAction.ReturnToConfiguredPoint;
                configStore.Save();
            }

            if (ImGui.Selectable($"{uiText.Get("settings.purchase.action.close_game")}##PostPurchaseCloseGame", postPurchaseAction == PostPurchaseAction.CloseGame))
            {
                configStore.pluginConfig.postPurchaseAction = PostPurchaseAction.CloseGame;
                configStore.Save();
            }

            ImGui.EndCombo();
        }

        var reserveScripAmount = configStore.pluginConfig.reserveScripAmount;
        var previousReserveScripAmount = reserveScripAmount;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.purchase.reserve"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##ReserveScripAmount", ref reserveScripAmount, 0, 0))
            configStore.pluginConfig.reserveScripAmount = SettingsValueRules.ClampNonNegative(reserveScripAmount);

        if (ImGui.IsItemDeactivatedAfterEdit() && configStore.pluginConfig.reserveScripAmount != previousReserveScripAmount)
            configStore.Save();

        var freeSlotThreshold = configStore.pluginConfig.freeSlotThreshold;
        var previousFreeSlotThreshold = freeSlotThreshold;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(uiText.Get("settings.purchase.free_slots"));
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(Math.Min(160f, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputInt("##FreeSlotThreshold", ref freeSlotThreshold, 0, 0))
            configStore.pluginConfig.freeSlotThreshold = SettingsValueRules.ClampNonNegative(freeSlotThreshold);

        if (ImGui.IsItemDeactivatedAfterEdit() && configStore.pluginConfig.freeSlotThreshold != previousFreeSlotThreshold)
            configStore.Save();

        ImGui.EndTable();
    }
}

internal static class SettingsValueRules
{
    internal static int ClampNonNegative(int value)
    {
        return Math.Max(0, value);
    }
}
