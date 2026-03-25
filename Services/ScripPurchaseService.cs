using ECommons.Automation.NeoTaskManager;
using Starloom.Data;
using Starloom.GameInterop.Addons;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Starloom.Services;

public sealed class ScripPurchaseService
{
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PurchaseTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShopWindowTimeout = TimeSpan.FromSeconds(30);
    private static readonly TaskManagerConfiguration NavigationTaskConfig = new(
        (int)TimeSpan.FromMinutes(2).TotalMilliseconds,
        true,
        true,
        false,
        false,
        true,
        true);

    private readonly ScripShopAddon shopAddon = new();
    private readonly Queue<PendingPurchaseItem> purchaseQueue = new();

    private DateTime lastActionAt;
    private DateTime purchaseStartedAt;
    private DateTime shopWaitStartedAt;
    private bool navigationStarted;
    private int currentPurchaseAmount;
    private uint currentTargetItemId;
    private string currentTargetItemName = string.Empty;
    private int inventoryCountBeforePurchase;

    public void Enqueue()
    {
        Reset();
        P.TM.Enqueue(PrepareQueue, "ScripPurchase.Prepare");
        P.TM.Enqueue(NavigateToShop, "ScripPurchase.Navigate", NavigationTaskConfig);
        P.TM.Enqueue(WaitForShop, "ScripPurchase.WaitShop");
        P.TM.Enqueue(ProcessNextPurchase, "ScripPurchase.Next");
        P.TM.Enqueue(Cleanup, "ScripPurchase.Cleanup");
    }

    public void Stop()
    {
        shopAddon.CloseShop();
        P.Navigation.Stop();
        Reset();
    }

    private void Reset()
    {
        purchaseQueue.Clear();
        lastActionAt = DateTime.MinValue;
        purchaseStartedAt = DateTime.MinValue;
        shopWaitStartedAt = DateTime.MinValue;
        navigationStarted = false;
        currentPurchaseAmount = 0;
        currentTargetItemId = 0;
        currentTargetItemName = string.Empty;
        inventoryCountBeforePurchase = 0;
    }

    private bool? PrepareQueue()
    {
        P.Inventory.InvalidateTransientCaches();
        if (C.PreferredCollectableShop == null)
            return Fail("Collectable shop is not configured.");

        purchaseQueue.Clear();
        foreach (var item in P.PurchaseResolver.ResolvePendingTargets())
            purchaseQueue.Enqueue(item);

        if (purchaseQueue.Count == 0)
            Svc.Log.Debug("No pending purchases, skipping.");

        return true;
    }

    private bool? NavigateToShop()
    {
        if (purchaseQueue.Count == 0)
            return true;

        var shop = C.PreferredCollectableShop!;
        var target = new NavigationTarget(
            shop.ScripShopLocation,
            shop.AetheryteId,
            shop.TerritoryId,
            0.4f,
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
            return Fail(P.Navigation.ErrorMessage ?? "Could not reach the scrip shop.");

        return false;
    }

    private bool? WaitForShop()
    {
        if (purchaseQueue.Count == 0)
            return true;

        if (shopAddon.IsReady)
        {
            lastActionAt = DateTime.MinValue;
            return true;
        }

        if (shopWaitStartedAt == DateTime.MinValue)
            shopWaitStartedAt = DateTime.UtcNow;

        if ((DateTime.UtcNow - shopWaitStartedAt) > ShopWindowTimeout)
            return Fail("Timed out while waiting for the scrip shop window.");

        if ((DateTime.UtcNow - lastActionAt) < TimeSpan.FromSeconds(1))
            return false;

        var shop = C.PreferredCollectableShop!;
        if (TryOpenScripShop())
            lastActionAt = DateTime.UtcNow;
        else if (P.NpcInteraction.TryInteract(shop.ScripShopNpcId))
            lastActionAt = DateTime.UtcNow;

        return false;
    }

    private unsafe bool TryOpenScripShop()
    {
        if (!TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("SelectIconString", out var addon))
            return false;
        if (!IsAddonReady(addon))
            return false;

        shopAddon.OpenShop();
        return true;
    }

    private bool? ProcessNextPurchase()
    {
        if (purchaseQueue.Count == 0)
            return true;

        P.TM.BeginStack();
        P.TM.Enqueue(SelectCurrentPage, "ScripPurchase.SelectPage");
        P.TM.Enqueue(SelectCurrentSubPage, "ScripPurchase.SelectSubPage");
        P.TM.Enqueue(SelectCurrentItem, "ScripPurchase.SelectItem");
        P.TM.Enqueue(PurchaseCurrentItem, "ScripPurchase.Purchase");
        P.TM.Enqueue(WaitForPurchaseCompletion, "ScripPurchase.WaitPurchase");
        P.TM.Enqueue(ProcessNextPurchase, "ScripPurchase.Next");
        P.TM.InsertStack();
        return true;
    }

    private bool? SelectCurrentPage()
    {
        if (purchaseQueue.Count == 0)
            return true;

        if (!ActionDelayElapsed())
            return false;

        shopAddon.SelectPage(purchaseQueue.Peek().Page);
        lastActionAt = DateTime.UtcNow;
        return true;
    }

    private bool? SelectCurrentSubPage()
    {
        if (purchaseQueue.Count == 0)
            return true;

        if (!ActionDelayElapsed())
            return false;

        shopAddon.SelectSubPage(purchaseQueue.Peek().SubPage);
        lastActionAt = DateTime.UtcNow;
        return true;
    }

    private bool? SelectCurrentItem()
    {
        if (purchaseQueue.Count == 0)
            return true;

        if (!ActionDelayElapsed())
            return false;

        var next = purchaseQueue.Peek();
        var scrips = P.Inventory.GetCurrencyItemCount(next.CurrencyItemId);
        if (scrips < 0)
            return Fail($"Could not read scrip count for {next.ItemName}");

        var availableScrips = Math.Max(0, scrips - C.ReserveScripAmount);
        var maxByScrip = next.ItemCost > 0 ? availableScrips / next.ItemCost : next.RemainingQuantity;
        var amount = Math.Min(next.RemainingQuantity, Math.Min(maxByScrip, 99));
        if (amount <= 0)
        {
            Svc.Log.Debug($"Skipping {next.ItemName}: not enough scrips (current={scrips}, reserve={C.ReserveScripAmount}, cost={next.ItemCost})");
            purchaseQueue.Dequeue();
            currentPurchaseAmount = 0;
            currentTargetItemId = 0;
            currentTargetItemName = string.Empty;
            inventoryCountBeforePurchase = 0;
            purchaseStartedAt = DateTime.MinValue;
            lastActionAt = DateTime.UtcNow;
            return true;
        }

        var knownShopItems = P.ShopItems.ShopItems.Count > 0
            ? P.ShopItems.ShopItems
            : C.ScripShopItems.Select(item => item.Item).ToList();

        if (!shopAddon.SelectItem(next.ItemId, next.ItemName, amount, knownShopItems))
            return Fail($"Could not locate the item in the scrip shop: {next.ItemName}");

        currentPurchaseAmount = amount;
        currentTargetItemId = next.ItemId;
        currentTargetItemName = next.ItemName;
        inventoryCountBeforePurchase = P.Inventory.GetInventoryItemCount(next.ItemId);
        purchaseStartedAt = DateTime.MinValue;
        lastActionAt = DateTime.UtcNow;
        return true;
    }

    private bool? PurchaseCurrentItem()
    {
        if (currentTargetItemId == 0)
            return true;

        if (!ActionDelayElapsed())
            return false;

        var result = shopAddon.PurchaseItem(currentTargetItemId, currentTargetItemName);
        switch (result)
        {
            case ScripShopAddon.PurchaseDialogResult.Missing:
                if (purchaseStartedAt == DateTime.MinValue)
                    purchaseStartedAt = DateTime.UtcNow;
                if ((DateTime.UtcNow - purchaseStartedAt) > PurchaseTimeout)
                    return Fail($"Purchase confirmation window did not appear: {currentTargetItemName}");
                return false;

            case ScripShopAddon.PurchaseDialogResult.MismatchedItem:
                return Fail($"Purchase confirmation item mismatch: {currentTargetItemName}");

            case ScripShopAddon.PurchaseDialogResult.Confirmed:
                purchaseStartedAt = DateTime.UtcNow;
                lastActionAt = DateTime.UtcNow;
                return true;

            default:
                return false;
        }
    }

    private bool? WaitForPurchaseCompletion()
    {
        if (currentTargetItemId == 0)
            return true;

        if (!ActionDelayElapsed())
            return false;

        var currentCount = P.Inventory.GetInventoryItemCount(currentTargetItemId);
        if (currentCount <= inventoryCountBeforePurchase)
        {
            if ((DateTime.UtcNow - purchaseStartedAt) > PurchaseTimeout)
                return Fail($"Scrip purchase did not complete: {currentTargetItemName}");

            return false;
        }

        var completedPurchase = purchaseQueue.Peek();
        purchaseQueue.Dequeue();
        var newRemaining = completedPurchase.RemainingQuantity - currentPurchaseAmount;
        if (newRemaining > 0)
            purchaseQueue.Enqueue(completedPurchase with { RemainingQuantity = newRemaining });

        currentPurchaseAmount = 0;
        currentTargetItemId = 0;
        currentTargetItemName = string.Empty;
        inventoryCountBeforePurchase = 0;
        purchaseStartedAt = DateTime.MinValue;
        lastActionAt = DateTime.UtcNow;
        return true;
    }

    private bool? Cleanup()
    {
        shopAddon.CloseShop();
        P.Inventory.InvalidateTransientCaches();
        P.Navigation.Stop();
        Svc.Log.Debug("Scrip purchase completed.");
        return true;
    }

    private bool? Fail(string message)
    {
        Svc.Log.Error($"Scrip purchase failed: {message}");
        shopAddon.CloseShop();
        P.Navigation.Stop();
        return null;
    }

    private bool ActionDelayElapsed()
        => (DateTime.UtcNow - lastActionAt) >= ActionDelay;
}
