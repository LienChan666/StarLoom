using Starloom.Data;
using Starloom.Services;

namespace Starloom.Automation;

internal static class WorkflowStartValidator
{
    internal static bool CanStartCollectable(out string errorMessage)
    {
        if (C.PreferredCollectableShop != null)
        {
            errorMessage = string.Empty;
            return true;
        }
        errorMessage = "A collectable shop must be configured before starting.";
        return false;
    }

    internal static bool CanStartPurchase(out string errorMessage)
    {
        if (C.ScripShopItems is not { Count: > 0 })
        {
            errorMessage = "The purchase list is empty.";
            return false;
        }

        var pendingItems = P.PurchaseResolver.Resolve();
        if (C.ScripShopItems.TrueForAll(item => item.Item == null || item.Quantity <= 0))
        {
            errorMessage = "The purchase list is empty or has no valid target quantities.";
            return false;
        }

        if (pendingItems.Count == 0)
        {
            errorMessage = "All configured purchase items already reached their target quantities.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    internal static bool CanStartArtisanList(out string errorMessage)
    {
        var configuredPoint = C.DefaultCraftReturnPoint ?? HousingReturnPoint.CreateInn();
        if (!HousingReturnPointService.TryResolveConfiguredPoint(configuredPoint, out var resolvedPoint))
        {
            errorMessage = "Failed to resolve the configured return point before starting Artisan.";
            return false;
        }

        if (resolvedPoint.IsInn)
        {
            if (HousingReturnPointService.IsInsideInn())
            {
                errorMessage = string.Empty;
                return true;
            }
            errorMessage = $"You are not currently at {resolvedPoint.DisplayName}, so the Artisan list cannot start.";
            return false;
        }

        if (HousingReturnPointService.IsInsideHouse() || Svc.ClientState.TerritoryType == resolvedPoint.TerritoryId)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"You are not currently at {resolvedPoint.DisplayName}, so the Artisan list cannot start.";
        return false;
    }
}
