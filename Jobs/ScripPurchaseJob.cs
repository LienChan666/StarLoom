using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using StarLoom.Addons;
using StarLoom.Core;
using StarLoom.Data;
using StarLoom.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace StarLoom.Jobs;

public sealed unsafe class ScripPurchaseJob : IAutomationJob
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
    private readonly Queue<(uint itemId, string name, int remaining, int cost, int page, int subPage, uint currencyItemId)> _purchaseQueue = new();
    private readonly TimeSpan _actionDelay = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _purchaseTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _shopWindowTimeout = TimeSpan.FromSeconds(15);
    private readonly StateMachine<PurchaseState> _stateMachine;

    private JobContext? _context;
    private PurchaseState _state = PurchaseState.Idle;
    private DateTime _lastAction = DateTime.MinValue;
    private DateTime _stateEnteredAt = DateTime.MinValue;
    private DateTime _purchaseStartedAt = DateTime.MinValue;
    private bool _navigationStarted;
    private int _currentPurchaseAmount;
    private uint _currentTargetItemId;
    private string _currentTargetItemName = string.Empty;
    private int _inventoryCountBeforePurchase;

    public string Id => "scrip-purchase";
    public JobStatus Status { get; private set; } = JobStatus.Idle;

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

    public bool CanStart() => true;

    public void Start(JobContext context)
    {
        _context = context;
        ResetRunState();
        InventoryService.InvalidateTransientCaches();
        if (context.Config.PreferredCollectableShop == null)
        {
            Fail("Collectable shop is not configured.");
            return;
        }

        Status = JobStatus.Running;
        TransitionTo(PurchaseState.PreparingQueue);
    }

    public void Update()
    {
        if (Status != JobStatus.Running || _context == null)
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

    public void Stop()
    {
        _context?.Navigation.Stop();
        _shopAddon.CloseShop();
        ResetRunState();
        Status = JobStatus.Idle;
    }

    private void ResetRunState()
    {
        _purchaseQueue.Clear();
        _lastAction = DateTime.MinValue;
        _stateEnteredAt = DateTime.MinValue;
        _purchaseStartedAt = DateTime.MinValue;
        _navigationStarted = false;
        _currentPurchaseAmount = 0;
        _currentTargetItemId = 0;
        _currentTargetItemName = string.Empty;
        _inventoryCountBeforePurchase = 0;
        TransitionTo(PurchaseState.Idle);
    }

    private void PreparePurchaseQueue()
    {
        _purchaseQueue.Clear();
        var configuredItems = _context!.Config.ScripShopItems;
        if (configuredItems == null)
        {
            Fail("Scrip purchase list is not configured.");
            return;
        }

        foreach (var item in configuredItems)
        {
            if (item.Item == null)
                continue;

            if (item.Item.Page >= 3)
                continue;

            var currentCount = InventoryService.GetInventoryItemCount(item.Item.ItemId);
            var remaining = item.Quantity - currentCount;
            if (remaining > 0)
            {
                _purchaseQueue.Enqueue((
                    item.Item.ItemId,
                    item.Name,
                    remaining,
                    (int)item.Item.ItemCost,
                    item.Item.Page,
                    item.Item.SubPage,
                    item.Item.CurrencyItemId));
            }
        }

        TransitionTo(_purchaseQueue.Count == 0 ? PurchaseState.Completed : PurchaseState.MovingToShop);
    }

    private void MoveToScripShop()
    {
        var shop = GetPreferredShop();
        if (!_navigationStarted)
        {
            _context!.Navigation.NavigateTo(new NavigationTarget(
                shop.ScripShopLocation,
                shop.AetheryteId,
                shop.TerritoryId,
                0.4f,
                shop.IsLifestreamRequired,
                shop.LifestreamCommand));
            _navigationStarted = true;
            return;
        }

        if (_context!.Navigation.State == NavigationService.NavigationState.Arrived)
        {
            _navigationStarted = false;
            TransitionTo(PurchaseState.WaitingForShopWindow);
            _lastAction = DateTime.MinValue;
            return;
        }

        if (_context.Navigation.State == NavigationService.NavigationState.Failed)
            Fail(_context.Navigation.ErrorMessage ?? "Could not reach the scrip shop.");
    }

    private void WaitForScripShopWindow()
    {
        if (_shopAddon.IsReady)
        {
            TransitionTo(PurchaseState.SelectingPage);
            _lastAction = DateTime.MinValue;
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > _shopWindowTimeout)
        {
            Fail("Timed out while waiting for the scrip shop window.");
            return;
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectIconString", out var selectAddon) && IsAddonReady(selectAddon))
        {
            _shopAddon.OpenShop();
            _lastAction = DateTime.UtcNow;
            return;
        }

        if ((DateTime.UtcNow - _lastAction) < TimeSpan.FromSeconds(1))
            return;

        if (_context!.NpcInteraction.TryInteract(GetPreferredShop().ScripShopNpcId))
            _lastAction = DateTime.UtcNow;
    }

    private void SelectScripShopPage()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        if (_purchaseQueue.Count == 0)
        {
            TransitionTo(PurchaseState.Completed);
            return;
        }

        var next = _purchaseQueue.Peek();
        _shopAddon.SelectPage(next.page);
        _lastAction = DateTime.UtcNow;
        TransitionTo(PurchaseState.SelectingSubPage);
    }

    private void SelectScripShopSubPage()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        var next = _purchaseQueue.Peek();
        _shopAddon.SelectSubPage(next.subPage);
        _lastAction = DateTime.UtcNow;
        TransitionTo(PurchaseState.SelectingItem);
    }

    private void SelectScripShopItem()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        var next = _purchaseQueue.Peek();
        var scrips = InventoryService.GetCurrencyItemCount(next.currencyItemId);
        if (scrips < 0)
        {
            Fail($"Could not read scrip count for {next.name}");
            return;
        }

        var availableScrips = Math.Max(0, scrips - _context!.Config.ReserveScripAmount);
        var maxByScrip = next.cost > 0 ? availableScrips / next.cost : next.remaining;
        var amount = Math.Min(next.remaining, Math.Min(maxByScrip, 99));
        if (amount <= 0)
        {
            Fail($"Not enough scrips to purchase one item: {next.name} (current={scrips}, reserve={_context.Config.ReserveScripAmount}, cost={next.cost})");
            return;
        }

        var knownShopItems = ScripShopItemManager.ShopItems.Count > 0
            ? ScripShopItemManager.ShopItems
            : _context.Config.ScripShopItems.Select(item => item.Item).ToList();

        if (!_shopAddon.SelectItem(next.itemId, next.name, amount, knownShopItems))
        {
            Fail($"Could not locate the item in the scrip shop: {next.name}");
            return;
        }

        _currentPurchaseAmount = amount;
        _currentTargetItemId = next.itemId;
        _currentTargetItemName = next.name;
        _inventoryCountBeforePurchase = InventoryService.GetInventoryItemCount(next.itemId);
        _purchaseStartedAt = DateTime.MinValue;
        _lastAction = DateTime.UtcNow;
        TransitionTo(PurchaseState.PurchasingItem);
    }

    private void PurchaseScripShopItem()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
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
                _lastAction = DateTime.UtcNow;
                TransitionTo(PurchaseState.WaitingForPurchaseComplete);
                return;
        }
    }

    private void WaitForPurchaseComplete()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        var currentCount = InventoryService.GetInventoryItemCount(_currentTargetItemId);
        if (currentCount <= _inventoryCountBeforePurchase)
        {
            if ((DateTime.UtcNow - _purchaseStartedAt) > _purchaseTimeout)
                Fail($"Scrip purchase did not complete: {_currentTargetItemName}");

            return;
        }

        var completedPurchase = _purchaseQueue.Peek();
        _purchaseQueue.Dequeue();
        var newRemaining = completedPurchase.remaining - _currentPurchaseAmount;
        if (newRemaining > 0)
            _purchaseQueue.Enqueue((completedPurchase.itemId, completedPurchase.name, newRemaining, completedPurchase.cost, completedPurchase.page, completedPurchase.subPage, completedPurchase.currencyItemId));

        _currentPurchaseAmount = 0;
        _currentTargetItemId = 0;
        _currentTargetItemName = string.Empty;
        _inventoryCountBeforePurchase = 0;
        _purchaseStartedAt = DateTime.MinValue;
        _lastAction = DateTime.UtcNow;
        TransitionTo(PurchaseState.CheckingForMore);
    }

    private void CheckForMorePurchases()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        TransitionTo(_purchaseQueue.Count > 0 ? PurchaseState.SelectingPage : PurchaseState.Completed);
    }

    private void Complete()
    {
        _context?.Navigation.Stop();
        _shopAddon.CloseShop();
        InventoryService.InvalidateTransientCaches();
        Status = JobStatus.Completed;
        TransitionTo(PurchaseState.Idle);
    }

    private void Fail(string message)
    {
        _context?.Navigation.Stop();
        _shopAddon.CloseShop();
        InventoryService.InvalidateTransientCaches();
        Status = JobStatus.Failed;
        Svc.Log.Error($"[Starloom] ScripPurchaseJob failed: {message}");
        TransitionTo(PurchaseState.Failed);
    }

    private void TransitionTo(PurchaseState state)
    {
        _stateMachine.TransitionTo(state);
        _stateEnteredAt = _stateMachine.StateEnteredAt;
    }

    private CollectableShop GetPreferredShop()
        => _context!.Config.PreferredCollectableShop!;
}






