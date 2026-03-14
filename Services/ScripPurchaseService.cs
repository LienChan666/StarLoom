using Starloom.Data;
using Starloom.GameInterop.Addons;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Starloom.Services;

public enum ScripPurchasePhase
{
    Idle,
    PreparingQueue,
    Navigating,
    WaitingForShop,
    SelectingPage,
    SelectingSubPage,
    SelectingItem,
    Purchasing,
    WaitingForPurchase,
    Cleanup,
    Done,
    Failed,
}

public sealed class ScripPurchaseService
{
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PurchaseTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShopWindowTimeout = TimeSpan.FromSeconds(30);

    private readonly ScripShopAddon shopAddon = new();
    private readonly Queue<PendingPurchaseItem> purchaseQueue = new();

    public string? ErrorMessage { get; private set; }
    public bool IsCompleted => phase == ScripPurchasePhase.Done;
    public bool HasFailed => phase == ScripPurchasePhase.Failed;

    private ScripPurchasePhase phase = ScripPurchasePhase.Idle;
    private DateTime lastActionAt;
    private DateTime purchaseStartedAt;
    private DateTime stateEnteredAt = DateTime.MinValue;
    private bool navigationStarted;
    private int currentPurchaseAmount;
    private uint currentTargetItemId;
    private string currentTargetItemName = string.Empty;
    private int inventoryCountBeforePurchase;

    public void Start()
    {
        purchaseQueue.Clear();
        lastActionAt = DateTime.MinValue;
        purchaseStartedAt = DateTime.MinValue;
        navigationStarted = false;
        currentPurchaseAmount = 0;
        currentTargetItemId = 0;
        currentTargetItemName = string.Empty;
        inventoryCountBeforePurchase = 0;
        ErrorMessage = null;
        TransitionTo(ScripPurchasePhase.PreparingQueue);
    }

    public void Stop()
    {
        shopAddon.CloseShop();
        purchaseQueue.Clear();
        ErrorMessage = null;
        TransitionTo(ScripPurchasePhase.Idle);
    }

    public void Advance()
    {
        if (phase is ScripPurchasePhase.Idle or ScripPurchasePhase.Done or ScripPurchasePhase.Failed)
            return;

        switch (phase)
        {
            case ScripPurchasePhase.PreparingQueue:
                HandlePrepareQueue();
                return;
            case ScripPurchasePhase.Navigating:
                HandleNavigating();
                return;
            case ScripPurchasePhase.WaitingForShop:
                HandleWaitingForShop();
                return;
            case ScripPurchasePhase.SelectingPage:
                HandleSelectingPage();
                return;
            case ScripPurchasePhase.SelectingSubPage:
                HandleSelectingSubPage();
                return;
            case ScripPurchasePhase.SelectingItem:
                HandleSelectingItem();
                return;
            case ScripPurchasePhase.Purchasing:
                HandlePurchasing();
                return;
            case ScripPurchasePhase.WaitingForPurchase:
                HandleWaitingForPurchase();
                return;
            case ScripPurchasePhase.Cleanup:
                HandleCleanup();
                return;
        }
    }

    private void HandlePrepareQueue()
    {
        P.Inventory.InvalidateTransientCaches();
        if (C.PreferredCollectableShop == null)
        {
            Fail("Collectable shop is not configured.");
            return;
        }

        purchaseQueue.Clear();
        foreach (var item in P.PurchaseResolver.ResolvePendingTargets())
            purchaseQueue.Enqueue(item);

        if (purchaseQueue.Count == 0)
        {
            Svc.Log.Debug("No pending purchases, skipping.");
            TransitionTo(ScripPurchasePhase.Done);
            return;
        }

        TransitionTo(ScripPurchasePhase.Navigating);
    }

    private void HandleNavigating()
    {
        var shop = C.PreferredCollectableShop!;
        var target = new NavigationTarget(
            shop.ScripShopLocation, shop.AetheryteId, shop.TerritoryId, 0.4f,
            shop.IsLifestreamRequired, shop.LifestreamCommand);

        if (!navigationStarted || P.Navigation.IsIdle)
        {
            P.Navigation.NavigateTo(target);
            navigationStarted = true;
            return;
        }

        P.Navigation.Poll();
        if (P.Navigation.IsComplete)
        {
            P.Navigation.Stop();
            navigationStarted = false;
            lastActionAt = DateTime.MinValue;
            TransitionTo(ScripPurchasePhase.WaitingForShop);
            return;
        }

        if (P.Navigation.HasFailed)
        {
            Fail(P.Navigation.ErrorMessage ?? "Could not reach the scrip shop.");
            return;
        }
    }

    private void HandleWaitingForShop()
    {
        if (shopAddon.IsReady)
        {
            lastActionAt = DateTime.MinValue;
            TransitionTo(ScripPurchasePhase.SelectingPage);
            return;
        }

        if (TimedOut(ShopWindowTimeout))
        {
            Fail("Timed out while waiting for the scrip shop window.");
            return;
        }

        if ((DateTime.UtcNow - lastActionAt) < TimeSpan.FromSeconds(1))
            return;

        var shop = C.PreferredCollectableShop!;
        if (TryOpenScripShop())
            lastActionAt = DateTime.UtcNow;
        else if (P.NpcInteraction.TryInteract(shop.ScripShopNpcId))
            lastActionAt = DateTime.UtcNow;
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

    private void HandleSelectingPage()
    {
        if (purchaseQueue.Count == 0) { TransitionTo(ScripPurchasePhase.Done); return; }
        if (!ActionDelayElapsed()) return;

        var next = purchaseQueue.Peek();
        shopAddon.SelectPage(next.Page);
        lastActionAt = DateTime.UtcNow;
        TransitionTo(ScripPurchasePhase.SelectingSubPage);
    }

    private void HandleSelectingSubPage()
    {
        if (purchaseQueue.Count == 0) { TransitionTo(ScripPurchasePhase.Done); return; }
        if (!ActionDelayElapsed()) return;

        var next = purchaseQueue.Peek();
        shopAddon.SelectSubPage(next.SubPage);
        lastActionAt = DateTime.UtcNow;
        TransitionTo(ScripPurchasePhase.SelectingItem);
    }

    private void HandleSelectingItem()
    {
        if (purchaseQueue.Count == 0) { TransitionTo(ScripPurchasePhase.Done); return; }
        if (!ActionDelayElapsed()) return;

        var next = purchaseQueue.Peek();
        var scrips = P.Inventory.GetCurrencyItemCount(next.CurrencyItemId);
        if (scrips < 0)
        {
            Fail($"Could not read scrip count for {next.ItemName}");
            return;
        }

        var availableScrips = Math.Max(0, scrips - C.ReserveScripAmount);
        var maxByScrip = next.ItemCost > 0 ? availableScrips / next.ItemCost : next.RemainingQuantity;
        var amount = Math.Min(next.RemainingQuantity, Math.Min(maxByScrip, 99));
        if (amount <= 0)
        {
            Svc.Log.Debug($"Skipping {next.ItemName}: not enough scrips (current={scrips}, reserve={C.ReserveScripAmount}, cost={next.ItemCost})");
            purchaseQueue.Dequeue();
            if (purchaseQueue.Count > 0)
                TransitionTo(ScripPurchasePhase.SelectingPage);
            else
                TransitionTo(ScripPurchasePhase.Cleanup);
            return;
        }

        var knownShopItems = P.ShopItems.ShopItems.Count > 0
            ? P.ShopItems.ShopItems
            : C.ScripShopItems.Select(item => item.Item).ToList();

        if (!shopAddon.SelectItem(next.ItemId, next.ItemName, amount, knownShopItems))
        {
            Fail($"Could not locate the item in the scrip shop: {next.ItemName}");
            return;
        }

        currentPurchaseAmount = amount;
        currentTargetItemId = next.ItemId;
        currentTargetItemName = next.ItemName;
        inventoryCountBeforePurchase = P.Inventory.GetInventoryItemCount(next.ItemId);
        purchaseStartedAt = DateTime.MinValue;
        lastActionAt = DateTime.UtcNow;
        TransitionTo(ScripPurchasePhase.Purchasing);
    }

    private void HandlePurchasing()
    {
        if (purchaseQueue.Count == 0) { TransitionTo(ScripPurchasePhase.Done); return; }
        if (!ActionDelayElapsed()) return;

        var result = shopAddon.PurchaseItem(currentTargetItemId, currentTargetItemName);
        switch (result)
        {
            case ScripShopAddon.PurchaseDialogResult.Missing:
                if (purchaseStartedAt == DateTime.MinValue)
                    purchaseStartedAt = DateTime.UtcNow;
                if ((DateTime.UtcNow - purchaseStartedAt) > PurchaseTimeout)
                {
                    Fail($"Purchase confirmation window did not appear: {currentTargetItemName}");
                    return;
                }
                return;

            case ScripShopAddon.PurchaseDialogResult.MismatchedItem:
                Fail($"Purchase confirmation item mismatch: {currentTargetItemName}");
                return;

            case ScripShopAddon.PurchaseDialogResult.Confirmed:
                purchaseStartedAt = DateTime.UtcNow;
                lastActionAt = DateTime.UtcNow;
                TransitionTo(ScripPurchasePhase.WaitingForPurchase);
                return;
        }
    }

    private void HandleWaitingForPurchase()
    {
        if (purchaseQueue.Count == 0) { TransitionTo(ScripPurchasePhase.Done); return; }
        if (!ActionDelayElapsed()) return;

        var currentCount = P.Inventory.GetInventoryItemCount(currentTargetItemId);
        if (currentCount <= inventoryCountBeforePurchase)
        {
            if ((DateTime.UtcNow - purchaseStartedAt) > PurchaseTimeout)
            {
                Fail($"Scrip purchase did not complete: {currentTargetItemName}");
                return;
            }
            return;
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

        if (purchaseQueue.Count > 0)
            TransitionTo(ScripPurchasePhase.SelectingPage);
        else
            TransitionTo(ScripPurchasePhase.Cleanup);
    }

    private void HandleCleanup()
    {
        shopAddon.CloseShop();
        P.Inventory.InvalidateTransientCaches();
        Svc.Log.Debug("Scrip purchase completed.");
        TransitionTo(ScripPurchasePhase.Done);
    }

    private void Fail(string message)
    {
        Svc.Log.Error($"Scrip purchase failed: {message}");
        ErrorMessage = message;
        shopAddon.CloseShop();
        TransitionTo(ScripPurchasePhase.Failed);
    }

    private bool ActionDelayElapsed()
        => (DateTime.UtcNow - lastActionAt) >= ActionDelay;

    private bool TimedOut(TimeSpan timeout)
        => (DateTime.UtcNow - stateEnteredAt) > timeout;

    private void TransitionTo(ScripPurchasePhase phase)
    {
        this.phase = phase;
        stateEnteredAt = DateTime.UtcNow;
    }
}
