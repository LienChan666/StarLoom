using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using StarLoom.Addons;
using StarLoom.Core;
using StarLoom.Data;
using StarLoom.Services;
using StarLoom.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace StarLoom.Jobs;

public sealed unsafe class ScripPurchaseJob : ShopJobBase
{
    private enum PurchaseState
    {
        Idle,
        PreparingQueue,
        MovingToShop,
        WaitingForShopWindow,
        SelectingPage,
        SelectingSubPage,
        SelectingItem,
        PurchasingItem,
        WaitingForPurchaseComplete,
        CheckingForMore,
        Completed,
        Failed,
    }

    private readonly ScripShopAddon _shopAddon = new();
    private readonly Queue<PendingPurchaseItem> _purchaseQueue = new();
    private readonly TimeSpan _actionDelay = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _purchaseTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _shopWindowTimeout = TimeSpan.FromSeconds(15);
    private readonly StateMachine<PurchaseState> _stateMachine;

    private PurchaseState _state = PurchaseState.Idle;
    private DateTime _purchaseStartedAt = DateTime.MinValue;
    private int _currentPurchaseAmount;
    private uint _currentTargetItemId;
    private string _currentTargetItemName = string.Empty;
    private int _inventoryCountBeforePurchase;

    public override string Id => "scrip-purchase";

    public ScripPurchaseJob()
    {
        _stateMachine = new StateMachine<PurchaseState>(PurchaseState.Idle, state => _state = state);
        _stateMachine.Configure(PurchaseState.PreparingQueue, PreparePurchaseQueue);
        _stateMachine.Configure(PurchaseState.MovingToShop, MoveToScripShop);
        _stateMachine.Configure(PurchaseState.WaitingForShopWindow, WaitForScripShopWindow);
        _stateMachine.Configure(PurchaseState.SelectingPage, SelectScripShopPage);
        _stateMachine.Configure(PurchaseState.SelectingSubPage, SelectScripShopSubPage);
        _stateMachine.Configure(PurchaseState.SelectingItem, SelectScripShopItem);
        _stateMachine.Configure(PurchaseState.PurchasingItem, PurchaseScripShopItem);
        _stateMachine.Configure(PurchaseState.WaitingForPurchaseComplete, WaitForPurchaseComplete);
        _stateMachine.Configure(PurchaseState.CheckingForMore, CheckForMorePurchases);
        _stateMachine.Configure(PurchaseState.Completed, Complete);
    }

    public override bool CanStart() => true;

    public override void Start(JobContext context)
    {
        base.Start(context);
        ResetRunState();
        context.Inventory.InvalidateTransientCaches();
        if (context.Config.PreferredCollectableShop == null)
        {
            Fail("Collectable shop is not configured.");
            return;
        }

        TransitionTo(PurchaseState.PreparingQueue);
    }

    public override void Update()
    {
        if (Status != JobStatus.Running || Context == null)
            return;

        try
        {
            _stateMachine.Update();
        }
        catch (Exception ex)
        {
            Fail($"Scrip purchase failed: {ex.Message}");
        }
    }

    public override void Stop()
    {
        _shopAddon.CloseShop();
        ResetRunState();
        base.Stop();
    }

    private void ResetRunState()
    {
        _purchaseQueue.Clear();
        LastActionAt = DateTime.MinValue;
        TransitionedAt = DateTime.MinValue;
        _purchaseStartedAt = DateTime.MinValue;
        NavigationStarted = false;
        _currentPurchaseAmount = 0;
        _currentTargetItemId = 0;
        _currentTargetItemName = string.Empty;
        _inventoryCountBeforePurchase = 0;
        TransitionTo(PurchaseState.Idle);
    }

    private void PreparePurchaseQueue()
    {
        _purchaseQueue.Clear();
        foreach (var item in new PendingPurchaseResolver(Context!.Config, Context.Inventory).Resolve())
            _purchaseQueue.Enqueue(item);

        TransitionTo(_purchaseQueue.Count == 0 ? PurchaseState.Completed : PurchaseState.MovingToShop);
    }

    private void MoveToScripShop()
    {
        var shop = GetPreferredShop();
        UpdateNavigationToShop(
            new ShopInteractionContext(
                new NavigationTarget(shop.ScripShopLocation, shop.AetheryteId, shop.TerritoryId, 0.4f, shop.IsLifestreamRequired, shop.LifestreamCommand),
                shop.ScripShopNpcId,
                "Could not reach the scrip shop.",
                "Timed out while waiting for the scrip shop window."),
            PurchaseState.WaitingForShopWindow,
            TransitionTo);
    }

    private void WaitForScripShopWindow()
    {
        var shop = GetPreferredShop();
        UpdateShopWindow(
            _shopAddon.IsReady,
            _shopWindowTimeout,
            new ShopInteractionContext(
                new NavigationTarget(shop.ScripShopLocation, shop.AetheryteId, shop.TerritoryId, 0.4f, shop.IsLifestreamRequired, shop.LifestreamCommand),
                shop.ScripShopNpcId,
                "Could not reach the scrip shop.",
                "Timed out while waiting for the scrip shop window."),
            PurchaseState.SelectingPage,
            TransitionTo,
            () => { _shopAddon.OpenShop(); return true; });
    }

    private void SelectScripShopPage()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        if (_purchaseQueue.Count == 0)
        {
            TransitionTo(PurchaseState.Completed);
            return;
        }

        var next = _purchaseQueue.Peek();
        _shopAddon.SelectPage(next.Page);
        LastActionAt = DateTime.UtcNow;
        TransitionTo(PurchaseState.SelectingSubPage);
    }

    private void SelectScripShopSubPage()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        var next = _purchaseQueue.Peek();
        _shopAddon.SelectSubPage(next.SubPage);
        LastActionAt = DateTime.UtcNow;
        TransitionTo(PurchaseState.SelectingItem);
    }

    private void SelectScripShopItem()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        var next = _purchaseQueue.Peek();
        var scrips = Context!.Inventory.GetCurrencyItemCount(next.CurrencyItemId);
        if (scrips < 0)
        {
            Fail($"Could not read scrip count for {next.ItemName}");
            return;
        }

        var availableScrips = Math.Max(0, scrips - Context!.Config.ReserveScripAmount);
        var maxByScrip = next.ItemCost > 0 ? availableScrips / next.ItemCost : next.RemainingQuantity;
        var amount = Math.Min(next.RemainingQuantity, Math.Min(maxByScrip, 99));
        if (amount <= 0)
        {
            Fail($"Not enough scrips to purchase one item: {next.ItemName} (current={scrips}, reserve={Context.Config.ReserveScripAmount}, cost={next.ItemCost})");
            return;
        }

        var knownShopItems = ScripShopItemManager.ShopItems.Count > 0
            ? ScripShopItemManager.ShopItems
            : Context.Config.ScripShopItems.Select(item => item.Item).ToList();

        if (!_shopAddon.SelectItem(next.ItemId, next.ItemName, amount, knownShopItems))
        {
            Fail($"Could not locate the item in the scrip shop: {next.ItemName}");
            return;
        }

        _currentPurchaseAmount = amount;
        _currentTargetItemId = next.ItemId;
        _currentTargetItemName = next.ItemName;
        _inventoryCountBeforePurchase = Context.Inventory.GetInventoryItemCount(next.ItemId);
        _purchaseStartedAt = DateTime.MinValue;
        LastActionAt = DateTime.UtcNow;
        TransitionTo(PurchaseState.PurchasingItem);
    }

    private void PurchaseScripShopItem()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        switch (_shopAddon.PurchaseItem(_currentTargetItemId, _currentTargetItemName))
        {
            case ScripShopAddon.PurchaseDialogResult.Missing:
                if (_purchaseStartedAt == DateTime.MinValue)
                    _purchaseStartedAt = DateTime.UtcNow;

                if ((DateTime.UtcNow - _purchaseStartedAt) > _purchaseTimeout)
                    Fail($"Purchase confirmation window did not appear: {_currentTargetItemName}");

                return;

            case ScripShopAddon.PurchaseDialogResult.MismatchedItem:
                Fail($"Purchase confirmation item mismatch: {_currentTargetItemName}");
                return;

            case ScripShopAddon.PurchaseDialogResult.Confirmed:
                _purchaseStartedAt = DateTime.UtcNow;
                LastActionAt = DateTime.UtcNow;
                TransitionTo(PurchaseState.WaitingForPurchaseComplete);
                return;
        }
    }

    private void WaitForPurchaseComplete()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        var currentCount = Context!.Inventory.GetInventoryItemCount(_currentTargetItemId);
        if (currentCount <= _inventoryCountBeforePurchase)
        {
            if ((DateTime.UtcNow - _purchaseStartedAt) > _purchaseTimeout)
                Fail($"Scrip purchase did not complete: {_currentTargetItemName}");

            return;
        }

        var completedPurchase = _purchaseQueue.Peek();
        _purchaseQueue.Dequeue();
        var newRemaining = completedPurchase.RemainingQuantity - _currentPurchaseAmount;
        if (newRemaining > 0)
            _purchaseQueue.Enqueue(completedPurchase with { RemainingQuantity = newRemaining });

        _currentPurchaseAmount = 0;
        _currentTargetItemId = 0;
        _currentTargetItemName = string.Empty;
        _inventoryCountBeforePurchase = 0;
        _purchaseStartedAt = DateTime.MinValue;
        LastActionAt = DateTime.UtcNow;
        TransitionTo(PurchaseState.CheckingForMore);
    }

    private void CheckForMorePurchases()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        TransitionTo(_purchaseQueue.Count > 0 ? PurchaseState.SelectingPage : PurchaseState.Completed);
    }

    private void Complete()
    {
        _shopAddon.CloseShop();
        Context?.Inventory.InvalidateTransientCaches();
        MarkCompleted();
        TransitionTo(PurchaseState.Idle);
    }

    private void Fail(string message)
    {
        _shopAddon.CloseShop();
        Context?.Inventory.InvalidateTransientCaches();
        FailJob(message);
        TransitionTo(PurchaseState.Failed);
    }

    private void TransitionTo(PurchaseState state)
    {
        _stateMachine.TransitionTo(state);
        TransitionedAt = _stateMachine.StateEnteredAt;
    }

    private CollectableShop GetPreferredShop()
        => Context!.Config.PreferredCollectableShop!;
}






