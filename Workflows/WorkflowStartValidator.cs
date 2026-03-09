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
}
