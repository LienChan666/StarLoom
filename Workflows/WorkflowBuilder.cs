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
    private readonly PendingPurchaseResolver _pendingPurchaseResolver;

    public WorkflowBuilder(Configuration config, IInventoryService inventory)
    {
        _config = config;
        _pendingPurchaseResolver = new PendingPurchaseResolver(config, inventory);
    }

    public IReadOnlyList<IAutomationJob> CreateConfiguredWorkflow()
    {
        var jobs = new List<IAutomationJob>
        {
            new CollectableTurnInJob(),
        };

        if (ShouldRunConfiguredPurchaseWorkflow())
            jobs.Add(new ScripPurchaseJob());

        jobs.Add(BuildPostPurchaseActionJob());

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
        => _config.BuyAfterEachTurnIn && _pendingPurchaseResolver.Resolve().Count > 0;

    private IAutomationJob BuildPostPurchaseActionJob()
        => _config.PostPurchaseAction switch
        {
            PurchaseCompletionAction.CloseGame => new CloseGameJob(),
            _ => new ReturnToCraftPointJob(),
        };
}
