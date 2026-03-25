using ECommons.Automation.NeoTaskManager;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using Starloom.GameInterop.Addons;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Starloom.Services;

public sealed class CollectableTurnInService
{
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ShopWindowTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SubmitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OvercapCheckWindow = TimeSpan.FromMilliseconds(500);
    private static readonly TaskManagerConfiguration NavigationTaskConfig = new(
        (int)TimeSpan.FromMinutes(2).TotalMilliseconds,
        true,
        true,
        false,
        false,
        true,
        true);

    private readonly CollectableShopAddon shopAddon = new();
    private readonly Queue<(uint itemId, string name, int count, int jobId)> turnInQueue = new();

    private uint currentItemId;
    private int currentJobId = -1;
    private int inventoryCountBeforeSubmit;
    private DateTime lastActionAt;
    private DateTime submitStartedAt;
    private DateTime shopWaitStartedAt;
    private bool navigationStarted;
    private bool overcapDetected;

    public void Enqueue()
    {
        Reset();
        P.TM.Enqueue(PrepareQueue, "TurnIn.Prepare");
        P.TM.Enqueue(NavigateToShop, "TurnIn.Navigate", NavigationTaskConfig);
        P.TM.Enqueue(WaitForShop, "TurnIn.WaitShop");
        P.TM.Enqueue(ProcessNextTurnIn, "TurnIn.Next");
        P.TM.Enqueue(Cleanup, "TurnIn.Cleanup");
    }

    public void Stop()
    {
        shopAddon.CloseWindow();
        P.Navigation.Stop();
        Reset();
    }

    private void Reset()
    {
        turnInQueue.Clear();
        currentItemId = 0;
        currentJobId = -1;
        inventoryCountBeforeSubmit = 0;
        lastActionAt = DateTime.MinValue;
        submitStartedAt = DateTime.MinValue;
        shopWaitStartedAt = DateTime.MinValue;
        navigationStarted = false;
        overcapDetected = false;
    }

    private bool? PrepareQueue()
    {
        P.Inventory.InvalidateTransientCaches();
        if (C.PreferredCollectableShop == null)
            return Fail("Collectable shop is not configured.");

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
            if (item == null || item.Value.RowId == 0)
                continue;

            var itemName = item.Value.Name.ToString();
            var jobId = ItemJobResolver.GetJobIdForItem(itemName, Svc.Data);
            if (jobId != -1)
                turnInQueue.Enqueue((itemId, itemName, count, jobId));
        }

        Svc.Log.Debug($"Found {turnInQueue.Count} collectable types to turn in.");
        return true;
    }

    private bool? NavigateToShop()
    {
        if (turnInQueue.Count == 0)
            return true;

        var shop = C.PreferredCollectableShop!;
        var target = new NavigationTarget(
            shop.Location,
            shop.AetheryteId,
            shop.TerritoryId,
            2f,
            shop.IsLifestreamRequired,
            shop.LifestreamCommand);

        if (!navigationStarted || P.Navigation.IsIdle)
        {
            P.Navigation.NavigateTo(target);
            navigationStarted = true;
            return false;
        }

        P.Navigation.Poll();
        if (P.Navigation.IsComplete)
        {
            P.Navigation.Stop();
            navigationStarted = false;
            lastActionAt = DateTime.MinValue;
            shopWaitStartedAt = DateTime.UtcNow;
            return true;
        }

        if (P.Navigation.HasFailed)
            return Fail(P.Navigation.ErrorMessage ?? "Could not reach the collectable shop.");

        return false;
    }

    private bool? WaitForShop()
    {
        if (turnInQueue.Count == 0)
            return true;

        if (shopAddon.IsReady)
        {
            lastActionAt = DateTime.MinValue;
            return true;
        }

        if (shopWaitStartedAt == DateTime.MinValue)
            shopWaitStartedAt = DateTime.UtcNow;

        if ((DateTime.UtcNow - shopWaitStartedAt) > ShopWindowTimeout)
            return Fail("Timed out while waiting for the collectable window.");

        if ((DateTime.UtcNow - lastActionAt) < TimeSpan.FromSeconds(1))
            return false;

        var shop = C.PreferredCollectableShop!;
        if (P.NpcInteraction.TryInteract(shop.NpcId))
            lastActionAt = DateTime.UtcNow;

        return false;
    }

    private bool? ProcessNextTurnIn()
    {
        if (turnInQueue.Count == 0 || overcapDetected)
            return true;

        P.TM.BeginStack();
        P.TM.Enqueue(SelectCurrentJob, "TurnIn.SelectJob");
        P.TM.Enqueue(SelectCurrentItem, "TurnIn.SelectItem");
        P.TM.Enqueue(SubmitCurrentItem, "TurnIn.Submit");
        P.TM.Enqueue(WaitForSubmit, "TurnIn.WaitSubmit");
        P.TM.Enqueue(ProcessNextTurnIn, "TurnIn.Next");
        P.TM.InsertStack();
        return true;
    }

    private bool? SelectCurrentJob()
    {
        if (turnInQueue.Count == 0 || overcapDetected)
            return true;

        if (!ActionDelayElapsed())
            return false;

        var next = turnInQueue.Peek();
        if (currentJobId != next.jobId)
        {
            shopAddon.SelectJob((uint)next.jobId);
            currentJobId = next.jobId;
            lastActionAt = DateTime.UtcNow;
            return false;
        }

        return true;
    }

    private bool? SelectCurrentItem()
    {
        if (turnInQueue.Count == 0 || overcapDetected)
            return true;

        if (!ActionDelayElapsed())
            return false;

        var next = turnInQueue.Peek();
        if (currentItemId != next.itemId)
        {
            shopAddon.SelectItemById(next.itemId);
            currentItemId = next.itemId;
            lastActionAt = DateTime.UtcNow;
            return false;
        }

        return true;
    }

    private bool? SubmitCurrentItem()
    {
        if (turnInQueue.Count == 0 || overcapDetected)
            return true;

        if (!ActionDelayElapsed())
            return false;

        P.Inventory.InvalidateTransientCaches();
        inventoryCountBeforeSubmit = P.Inventory.GetCollectableInventoryItemCount(currentItemId);
        submitStartedAt = DateTime.UtcNow;
        shopAddon.SubmitItem();
        lastActionAt = DateTime.UtcNow;
        return true;
    }

    private bool? WaitForSubmit()
    {
        if (turnInQueue.Count == 0)
            return true;

        if ((DateTime.UtcNow - submitStartedAt) < OvercapCheckWindow)
        {
            if (TryDismissOvercapDialog())
                overcapDetected = true;

            return overcapDetected ? true : false;
        }

        var next = turnInQueue.Peek();
        var currentCount = P.Inventory.GetCollectableInventoryItemCount(next.itemId);
        if (currentCount >= inventoryCountBeforeSubmit)
        {
            if ((DateTime.UtcNow - submitStartedAt) > SubmitTimeout)
                return Fail($"Collectable submission did not complete: {next.name}");

            return false;
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
        return true;
    }

    private unsafe bool TryDismissOvercapDialog()
    {
        if (!TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) || !IsAddonReady(&addon->AtkUnitBase))
            return false;

        new AddonMaster.SelectYesno((IntPtr)addon).No();
        return true;
    }

    private bool? Cleanup()
    {
        shopAddon.CloseWindow();
        P.Inventory.InvalidateTransientCaches();
        P.Navigation.Stop();
        Svc.Log.Debug("Collectable turn-in completed.");
        return true;
    }

    private bool? Fail(string message)
    {
        Svc.Log.Error($"Collectable turn-in failed: {message}");
        shopAddon.CloseWindow();
        P.Navigation.Stop();
        return null;
    }

    private bool ActionDelayElapsed()
        => (DateTime.UtcNow - lastActionAt) >= ActionDelay;
}
