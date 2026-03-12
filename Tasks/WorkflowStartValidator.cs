using StarLoom.Config;
using StarLoom.Game;
using StarLoom.Tasks.Artisan;
using StarLoom.Tasks.Purchase;

namespace StarLoom.Tasks;

internal static class WorkflowStartValidator
{
    internal static bool CanStartCollectable(PluginConfig pluginConfig, out string errorMessage)
    {
        if (pluginConfig.preferredCollectableShop != null)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = "A collectable shop must be configured before starting.";
        return false;
    }

    internal static bool CanStartPurchase(PluginConfig pluginConfig, InventoryGame inventoryGame, out string errorMessage)
    {
        if (pluginConfig.scripShopItems is not { Count: > 0 })
        {
            errorMessage = "The purchase list is empty.";
            return false;
        }

        if (!HasValidPurchaseTargets(pluginConfig))
        {
            errorMessage = "The purchase list is empty or has no valid target quantities.";
            return false;
        }

        if (!HasPendingPurchaseWork(pluginConfig, inventoryGame))
        {
            errorMessage = "All configured purchase items already reached their target quantities.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    internal static bool CanStartArtisanList(ArtisanSnapshot snapshot, bool artisanListManaged, out string errorMessage)
    {
        if (!snapshot.isAvailable)
        {
            errorMessage = "Artisan IPC is unavailable.";
            return false;
        }

        if (snapshot.listId <= 0)
        {
            errorMessage = "Artisan list id is required.";
            return false;
        }

        if (artisanListManaged && snapshot.isListRunning)
        {
            errorMessage = string.Empty;
            return true;
        }

        if (snapshot.isListRunning || snapshot.isBusy || snapshot.hasEnduranceEnabled)
        {
            errorMessage = "Artisan is busy with another task.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    internal static bool HasPendingPurchaseWork(PluginConfig pluginConfig, InventoryGame inventoryGame)
    {
        return new PendingPurchaseResolver(pluginConfig, inventoryGame).HasPending();
    }

    private static bool HasValidPurchaseTargets(PluginConfig pluginConfig)
    {
        return pluginConfig.scripShopItems.Any(item => item.itemId > 0 && item.targetCount > 0);
    }
}
