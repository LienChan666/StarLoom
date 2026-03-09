using StarLoom.Core;
using StarLoom.Data;
using StarLoom.Jobs;
using StarLoom.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace StarLoom.Workflows;

public sealed class WorkflowBuilder
{
    private readonly Configuration _config;
    private readonly IInventoryService _inventory;

    public WorkflowBuilder(Configuration config, IInventoryService inventory)
    {
        _config = config;
        _inventory = inventory;
    }

    public IReadOnlyList<IAutomationJob> CreateConfiguredWorkflow()
    {
        var jobs = new List<IAutomationJob>
        {
            new CollectableTurnInJob(),
        };

        if (ShouldRunConfiguredPurchaseWorkflow())
        {
            jobs.Add(new ScripPurchaseJob());
            jobs.Add(BuildPostPurchaseActionJob());
        }

        return jobs;
    }

    public IReadOnlyList<IAutomationJob> CreatePurchaseWorkflow()
    {
        var jobs = new List<IAutomationJob>
        {
            new ScripPurchaseJob(),
            BuildPostPurchaseActionJob(),
        };

        return jobs;
    }

    public bool ShouldRunConfiguredPurchaseWorkflow()
        => _config.BuyAfterEachTurnIn && GetPendingPurchaseItems().Count > 0;

    public List<ItemToPurchase> GetPendingPurchaseItems()
        => _config.ScripShopItems
            .Where(item => item.Item != null && item.Quantity > 0)
            .Where(item => _inventory.GetInventoryItemCount(item.Item!.ItemId) < item.Quantity)
            .ToList();

    private IAutomationJob BuildPostPurchaseActionJob()
        => _config.PostPurchaseAction switch
        {
            PurchaseCompletionAction.CloseGame => new CloseGameJob(),
            _ => new ReturnToCraftPointJob(),
        };
}
