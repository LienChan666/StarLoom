using ECommons.DalamudServices;
using StarLoom.Services;
using StarLoom.Services.Interfaces;

namespace StarLoom.Workflows;

public sealed class WorkflowStartValidator
{
    private readonly Configuration _config;
    private readonly PendingPurchaseResolver _pendingPurchaseResolver;

    public WorkflowStartValidator(Configuration config, IInventoryService inventory)
    {
        _config = config;
        _pendingPurchaseResolver = new PendingPurchaseResolver(config, inventory);
    }

    public bool CanStartCollectableWorkflow(out string errorMessage)
    {
        if (_config.PreferredCollectableShop != null)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = "A collectable shop must be configured before starting.";
        return false;
    }

    public bool CanStartPurchaseWorkflow(out string errorMessage)
    {
        if (_config.ScripShopItems is not { Count: > 0 })
        {
            errorMessage = "The purchase list is empty.";
            return false;
        }

        var pendingPurchaseItems = _pendingPurchaseResolver.Resolve();

        if (_config.ScripShopItems.TrueForAll(item => item.Item == null || item.Quantity <= 0))
        {
            errorMessage = "The purchase list is empty or has no valid target quantities.";
            return false;
        }

        if (pendingPurchaseItems.Count == 0)
        {
            errorMessage = "All configured purchase items already reached their target quantities.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool CanStartArtisanList(out string errorMessage)
    {
        var configuredPoint = _config.DefaultCraftReturnPoint ?? Data.HousingReturnPoint.CreateInn();
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
