using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using Starloom.Automation;
using Starloom.GameInterop.Addons;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Starloom.Services;

public enum CollectableTurnInState
{
    Idle,
    CheckingInventory,
    Navigating,
    WaitingForShop,
    SelectingJob,
    SelectingItem,
    Submitting,
    WaitingForSubmit,
    Cleanup,
    Done,
    Failed,
}

public sealed class CollectableTurnInService
{
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ShopWindowTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SubmitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OvercapCheckWindow = TimeSpan.FromMilliseconds(500);

    private readonly CollectableShopAddon shopAddon = new();
    private readonly StateMachine<CollectableTurnInState> stateMachine;
    private readonly Queue<(uint itemId, string name, int count, int jobId)> turnInQueue = new();

    public CollectableTurnInState State { get; private set; } = CollectableTurnInState.Idle;
    public string? ErrorMessage { get; private set; }

    private uint currentItemId;
    private int currentJobId = -1;
    private int inventoryCountBeforeSubmit;
    private DateTime lastActionAt;
    private DateTime submitStartedAt;
    private bool navigationStarted;
    private bool overcapDetected;

    public CollectableTurnInService()
    {
        stateMachine = new StateMachine<CollectableTurnInState>(CollectableTurnInState.Idle, state => State = state);
        stateMachine.Configure(CollectableTurnInState.CheckingInventory, HandleCheckingInventory);
        stateMachine.Configure(CollectableTurnInState.Navigating, HandleNavigating);
        stateMachine.Configure(CollectableTurnInState.WaitingForShop, HandleWaitingForShop);
        stateMachine.Configure(CollectableTurnInState.SelectingJob, HandleSelectingJob);
        stateMachine.Configure(CollectableTurnInState.SelectingItem, HandleSelectingItem);
        stateMachine.Configure(CollectableTurnInState.Submitting, HandleSubmitting);
        stateMachine.Configure(CollectableTurnInState.WaitingForSubmit, HandleWaitingForSubmit);
        stateMachine.Configure(CollectableTurnInState.Cleanup, HandleCleanup);
    }

    public void Start()
    {
        turnInQueue.Clear();
        currentItemId = 0;
        currentJobId = -1;
        inventoryCountBeforeSubmit = 0;
        lastActionAt = DateTime.MinValue;
        submitStartedAt = DateTime.MinValue;
        navigationStarted = false;
        overcapDetected = false;
        ErrorMessage = null;
        TransitionTo(CollectableTurnInState.CheckingInventory);
    }

    public void Stop()
    {
        shopAddon.CloseWindow();
        P.Navigation.Stop();
        turnInQueue.Clear();
        ErrorMessage = null;
        TransitionTo(CollectableTurnInState.Idle);
    }

    public void Update()
    {
        if (State is CollectableTurnInState.Idle or CollectableTurnInState.Done or CollectableTurnInState.Failed)
            return;

        stateMachine.Update();
    }

    private void HandleCheckingInventory()
    {
        P.Inventory.InvalidateTransientCaches();
        if (C.PreferredCollectableShop == null)
        {
            Fail("Collectable shop is not configured.");
            return;
        }

        var collectables = P.Inventory.GetCurrentInventoryItems()
            .Where(item => item.IsCollectable && P.Inventory.IsCollectableTurnInItem(item.BaseItemId))
            .GroupBy(item => item.BaseItemId)
            .ToList();

        turnInQueue.Clear();
        foreach (var group in collectables)
        {
            var itemId = group.Key;
            var count = group.Sum(item => (int)item.Quantity);
            var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
            if (item == null || item.Value.RowId == 0) continue;

            var itemName = item.Value.Name.ToString();
            var jobId = ItemJobResolver.GetJobIdForItem(itemName, Svc.Data);
            if (jobId != -1)
                turnInQueue.Enqueue((itemId, itemName, count, jobId));
        }

        Svc.Log.Debug($"Found {turnInQueue.Count} collectable types to turn in.");
        if (turnInQueue.Count == 0)
        {
            Svc.Log.Debug("No collectables to turn in, skipping.");
            TransitionTo(CollectableTurnInState.Done);
            return;
        }

        TransitionTo(CollectableTurnInState.Navigating);
    }

    private void HandleNavigating()
    {
        var shop = C.PreferredCollectableShop!;
        var target = new NavigationTarget(
            shop.Location, shop.AetheryteId, shop.TerritoryId, 2f,
            shop.IsLifestreamRequired, shop.LifestreamCommand);

        if (!navigationStarted || P.Navigation.State == NavigationStatus.Idle)
        {
            P.Navigation.NavigateTo(target);
            navigationStarted = true;
            return;
        }

        switch (P.Navigation.State)
        {
            case NavigationStatus.Arrived:
                navigationStarted = false;
                lastActionAt = DateTime.MinValue;
                TransitionTo(CollectableTurnInState.WaitingForShop);
                return;
            case NavigationStatus.Failed:
                Fail(P.Navigation.ErrorMessage ?? "Could not reach the collectable shop.");
                return;
        }
    }

    private void HandleWaitingForShop()
    {
        if (shopAddon.IsReady)
        {
            lastActionAt = DateTime.MinValue;
            TransitionTo(CollectableTurnInState.SelectingJob);
            return;
        }

        if (stateMachine.TimedOut(ShopWindowTimeout))
        {
            Fail("Timed out while waiting for the collectable window.");
            return;
        }

        if ((DateTime.UtcNow - lastActionAt) < TimeSpan.FromSeconds(1))
            return;

        var shop = C.PreferredCollectableShop!;
        if (P.NpcInteraction.TryInteract(shop.NpcId))
            lastActionAt = DateTime.UtcNow;
    }

    private void HandleSelectingJob()
    {
        if (turnInQueue.Count == 0 || overcapDetected) { TransitionTo(CollectableTurnInState.Cleanup); return; }
        if (!ActionDelayElapsed()) return;

        var next = turnInQueue.Peek();
        if (currentJobId == next.jobId)
        {
            TransitionTo(CollectableTurnInState.SelectingItem);
            return;
        }

        shopAddon.SelectJob((uint)next.jobId);
        currentJobId = next.jobId;
        lastActionAt = DateTime.UtcNow;
        TransitionTo(CollectableTurnInState.SelectingItem);
    }

    private void HandleSelectingItem()
    {
        if (turnInQueue.Count == 0 || overcapDetected) { TransitionTo(CollectableTurnInState.Cleanup); return; }
        if (!ActionDelayElapsed()) return;

        var next = turnInQueue.Peek();
        if (currentItemId == next.itemId)
        {
            TransitionTo(CollectableTurnInState.Submitting);
            return;
        }

        shopAddon.SelectItemById(next.itemId);
        currentItemId = next.itemId;
        lastActionAt = DateTime.UtcNow;
        TransitionTo(CollectableTurnInState.Submitting);
    }

    private void HandleSubmitting()
    {
        if (turnInQueue.Count == 0) { TransitionTo(CollectableTurnInState.Cleanup); return; }
        if (!ActionDelayElapsed()) return;

        P.Inventory.InvalidateTransientCaches();
        inventoryCountBeforeSubmit = P.Inventory.GetCollectableInventoryItemCount(currentItemId);
        submitStartedAt = DateTime.UtcNow;
        shopAddon.SubmitItem();
        lastActionAt = DateTime.UtcNow;
        TransitionTo(CollectableTurnInState.WaitingForSubmit);
    }

    private void HandleWaitingForSubmit()
    {
        if (turnInQueue.Count == 0) { TransitionTo(CollectableTurnInState.Cleanup); return; }

        if ((DateTime.UtcNow - submitStartedAt) < OvercapCheckWindow)
        {
            if (TryDismissOvercapDialog())
            {
                overcapDetected = true;
                TransitionTo(CollectableTurnInState.Cleanup);
            }
            return;
        }

        var next = turnInQueue.Peek();
        var currentCount = P.Inventory.GetCollectableInventoryItemCount(next.itemId);
        if (currentCount >= inventoryCountBeforeSubmit)
        {
            if ((DateTime.UtcNow - submitStartedAt) > SubmitTimeout)
            {
                Fail($"Collectable submission did not complete: {next.name}");
                return;
            }
            return;
        }

        var newCount = next.count - 1;
        turnInQueue.Dequeue();

        if (newCount > 0)
        {
            var remaining = turnInQueue.ToList();
            turnInQueue.Clear();
            turnInQueue.Enqueue((next.itemId, next.name, newCount, next.jobId));
            foreach (var item in remaining)
                turnInQueue.Enqueue(item);
        }
        else
        {
            currentItemId = 0;
        }

        inventoryCountBeforeSubmit = 0;
        submitStartedAt = DateTime.MinValue;
        lastActionAt = DateTime.UtcNow;

        if (turnInQueue.Count > 0)
            TransitionTo(CollectableTurnInState.SelectingJob);
        else
            TransitionTo(CollectableTurnInState.Cleanup);
    }

    private unsafe bool TryDismissOvercapDialog()
    {
        if (!TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) || !IsAddonReady(&addon->AtkUnitBase))
            return false;

        new AddonMaster.SelectYesno((IntPtr)addon).No();
        return true;
    }

    private void HandleCleanup()
    {
        shopAddon.CloseWindow();
        P.Inventory.InvalidateTransientCaches();
        P.Navigation.Stop();
        Svc.Log.Debug("Collectable turn-in completed.");
        TransitionTo(CollectableTurnInState.Done);
    }

    private void Fail(string message)
    {
        Svc.Log.Error($"Collectable turn-in failed: {message}");
        ErrorMessage = message;
        shopAddon.CloseWindow();
        TransitionTo(CollectableTurnInState.Failed);
    }

    private bool ActionDelayElapsed()
        => (DateTime.UtcNow - lastActionAt) >= ActionDelay;

    private void TransitionTo(CollectableTurnInState state)
        => stateMachine.TransitionTo(state);
}
