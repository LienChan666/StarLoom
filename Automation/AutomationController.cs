using Starloom.Tasks;
using System;

namespace Starloom.Automation;

internal sealed class AutomationController : IDisposable
{
    internal bool IsBusy => P.TM.IsBusy || P.Session.IsActive;
    internal bool HasConfiguredPurchases => C.ScripShopItems is { Count: > 0 };

    internal void StartConfiguredWorkflow()
    {
        if (IsBusy) return;
        if (!WorkflowStartValidator.CanStartCollectable(out var error))
        {
            DuoLog.Error(error);
            return;
        }
        if (P.Session.TryStart())
            Svc.Log.Info("Started managed Artisan session.");
    }

    internal void StartCollectableTurnIn()
    {
        if (IsBusy) return;
        Svc.Log.Info("Started collectable turn-in.");
        Workflows.EnqueueTurnInOnly();
    }

    internal void StartPurchaseOnly()
    {
        if (IsBusy) return;
        if (!WorkflowStartValidator.CanStartPurchase(out var error))
        {
            DuoLog.Error(error);
            return;
        }
        Svc.Log.Info("Started purchase workflow.");
        Workflows.EnqueuePurchaseWorkflow();
    }

    internal void Stop()
    {
        Svc.Log.Info("Stop requested.");
        P.TM.Abort();
        P.Session.Stop();
        P.CollectableTurnIn.Stop();
        P.ScripPurchase.Stop();
        P.Navigation.Stop();
    }

    internal void Update()
    {
        P.Session.Update();
    }

    public void Dispose()
    {
        if (IsBusy) Stop();
    }
}
