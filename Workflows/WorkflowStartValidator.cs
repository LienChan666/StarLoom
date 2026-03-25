using ECommons.DalamudServices;
using StarLoom.Services;
using StarLoom.Services.Interfaces;
using System.Linq;

namespace StarLoom.Workflows;

public sealed class WorkflowStartValidator
{
    private readonly Configuration _config;
    private readonly IInventoryService _inventory;

    public WorkflowStartValidator(Configuration config, IInventoryService inventory)
    {
        _config = config;
        _inventory = inventory;
    }

    public bool CanStartCollectableWorkflow(out string errorMessage)
    {
        if (_config.PreferredCollectableShop != null)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = "启动前需要先配置收藏品商店。";
        return false;
    }

    public bool CanStartPurchaseWorkflow(out string errorMessage)
    {
        if (_config.ScripShopItems is not { Count: > 0 })
        {
            errorMessage = "启动前，兑换物品列表为空。";
            return false;
        }

        var validPurchaseItems = _config.ScripShopItems
            .Where(item => item.Item != null && item.Quantity > 0)
            .ToList();

        if (validPurchaseItems.Count == 0)
        {
            errorMessage = "启动前，兑换物品列表为空或没有有效目标数量。";
            return false;
        }

        var pending = validPurchaseItems
            .Where(item => _inventory.GetInventoryItemCount(item.Item!.ItemId) < item.Quantity)
            .ToList();

        if (pending.Count == 0)
        {
            errorMessage = "启动前，兑换清单中的所有物品都已达到目标数量。";
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
            errorMessage = "\u542f\u52a8 Artisan \u524d\u65e0\u6cd5\u89e3\u6790\u914d\u7f6e\u7684\u8fd4\u56de\u70b9\uff0c\u8bf7\u91cd\u65b0\u9009\u62e9\u3002";
            return false;
        }

        if (resolvedPoint.IsInn)
        {
            if (HousingReturnPointService.IsInsideInn())
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = $"\u5f53\u524d\u4e0d\u5728 {resolvedPoint.DisplayName}\uff0c\u65e0\u6cd5\u542f\u52a8 Artisan \u6e05\u5355\u3002";
            return false;
        }

        if (HousingReturnPointService.IsInsideHouse() || Svc.ClientState.TerritoryType == resolvedPoint.TerritoryId)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"\u5f53\u524d\u4e0d\u5728 {resolvedPoint.DisplayName}\uff0c\u65e0\u6cd5\u542f\u52a8 Artisan \u6e05\u5355\u3002";
        return false;
    }
}