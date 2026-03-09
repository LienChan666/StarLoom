using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using StarLoom.Addons;
using StarLoom.Core;
using StarLoom.Data;
using StarLoom.Services;
using StarLoom.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace StarLoom.Jobs;

public sealed unsafe class CollectableTurnInJob : ShopJobBase
{
    private enum TurnInState
    {
        Idle,
        CheckingInventory,
        MovingToShop,
        WaitingForShopWindow,
        SelectingJob,
        SelectingItem,
        SubmittingItem,
        CheckingOvercapDialog,
        WaitingForSubmit,
        CheckingForMore,
        Completed,
        Failed,
    }

    private readonly CollectableShopAddon _shopAddon = new();
    private readonly Queue<(uint itemId, string name, int count, int jobId)> _turnInQueue = new();
    private readonly TimeSpan _actionDelay = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _shopWindowTimeout = TimeSpan.FromSeconds(15);
    private readonly TimeSpan _submitTimeout = TimeSpan.FromSeconds(5);
    private readonly StateMachine<TurnInState> _stateMachine;

    private TurnInState _state = TurnInState.Idle;
    private uint _currentItemId;
    private int _currentJobId = -1;
    private DateTime _overcapCheckStartedAt = DateTime.MinValue;
    private DateTime _submitStartedAt = DateTime.MinValue;
    private int _inventoryCountBeforeSubmit;

    public override string Id => "collectable-turn-in";
    public bool OvercapDetected { get; private set; }

    public CollectableTurnInJob()
    {
        _stateMachine = new StateMachine<TurnInState>(TurnInState.Idle, state => _state = state);
        _stateMachine.Configure(TurnInState.CheckingInventory, CheckInventory);
        _stateMachine.Configure(TurnInState.MovingToShop, MoveToShop);
        _stateMachine.Configure(TurnInState.WaitingForShopWindow, WaitForShopWindow);
        _stateMachine.Configure(TurnInState.SelectingJob, SelectJob);
        _stateMachine.Configure(TurnInState.SelectingItem, SelectItem);
        _stateMachine.Configure(TurnInState.SubmittingItem, SubmitItem);
        _stateMachine.Configure(TurnInState.CheckingOvercapDialog, CheckOvercapDialog);
        _stateMachine.Configure(TurnInState.WaitingForSubmit, WaitForSubmit);
        _stateMachine.Configure(TurnInState.CheckingForMore, CheckForMore);
        _stateMachine.Configure(TurnInState.Completed, Complete);
    }

    public override bool CanStart() => Context?.Inventory.HasCollectableTurnIns() ?? true;

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

        TransitionTo(TurnInState.CheckingInventory);
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
            Fail($"Collectable turn-in failed: {ex.Message}");
        }
    }

    public override void Stop()
    {
        _shopAddon.CloseWindow();
        ResetRunState();
        base.Stop();
    }

    private void ResetRunState()
    {
        _turnInQueue.Clear();
        _currentItemId = 0;
        _currentJobId = -1;
        LastActionAt = DateTime.MinValue;
        TransitionedAt = DateTime.MinValue;
        _overcapCheckStartedAt = DateTime.MinValue;
        _submitStartedAt = DateTime.MinValue;
        NavigationStarted = false;
        _inventoryCountBeforeSubmit = 0;
        OvercapDetected = false;
        TransitionTo(TurnInState.Idle);
    }

    private void CheckInventory()
    {
        var collectables = Context!.Inventory.GetCurrentInventoryItems()
            .Where(item => item.IsCollectable && Context.Inventory.IsCollectableTurnInItem(item.BaseItemId))
            .GroupBy(item => item.BaseItemId)
            .ToList();

        _turnInQueue.Clear();
        foreach (var group in collectables)
        {
            var itemId = group.Key;
            var count = group.Sum(item => (int)item.Quantity);
            var item = ECommons.DalamudServices.Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
            if (item == null || item.Value.RowId == 0)
                continue;

            var itemName = item.Value.Name.ToString();
            var jobId = ItemJobResolver.GetJobIdForItem(itemName, ECommons.DalamudServices.Svc.Data);
            if (jobId != -1)
                _turnInQueue.Enqueue((itemId, itemName, count, jobId));
        }

        TransitionTo(_turnInQueue.Count == 0 ? TurnInState.Completed : TurnInState.MovingToShop);
    }

    private void MoveToShop()
    {
        var shop = GetPreferredShop();
        UpdateNavigationToShop(
            new ShopInteractionContext(
                new NavigationTarget(shop.Location, shop.AetheryteId, shop.TerritoryId, 2f, shop.IsLifestreamRequired, shop.LifestreamCommand),
                shop.NpcId,
                "Could not reach the collectable NPC.",
                "Timed out while waiting for the collectable window."),
            TurnInState.WaitingForShopWindow,
            TransitionTo);
    }

    private void WaitForShopWindow()
    {
        var shop = GetPreferredShop();
        UpdateShopWindow(
            _shopAddon.IsReady,
            _shopWindowTimeout,
            new ShopInteractionContext(
                new NavigationTarget(shop.Location, shop.AetheryteId, shop.TerritoryId, 2f, shop.IsLifestreamRequired, shop.LifestreamCommand),
                shop.NpcId,
                "Could not reach the collectable NPC.",
                "Timed out while waiting for the collectable window."),
            TurnInState.SelectingJob,
            TransitionTo);
    }

    private void SelectJob()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        if (_turnInQueue.Count == 0)
        {
            TransitionTo(TurnInState.Completed);
            return;
        }

        var next = _turnInQueue.Peek();
        if (_currentJobId != next.jobId)
        {
            _shopAddon.SelectJob((uint)next.jobId);
            _currentJobId = next.jobId;
            LastActionAt = DateTime.UtcNow;
        }

        TransitionTo(TurnInState.SelectingItem);
    }

    private void SelectItem()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        var next = _turnInQueue.Peek();
        if (_currentItemId != next.itemId)
        {
            _shopAddon.SelectItemById(next.itemId);
            _currentItemId = next.itemId;
            LastActionAt = DateTime.UtcNow;
            return;
        }

        TransitionTo(TurnInState.SubmittingItem);
    }

    private void SubmitItem()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        Context!.Inventory.InvalidateTransientCaches();
        _inventoryCountBeforeSubmit = Context.Inventory.GetCollectableInventoryItemCount(_currentItemId);
        _submitStartedAt = DateTime.UtcNow;
        _shopAddon.SubmitItem();
        LastActionAt = DateTime.UtcNow;
        _overcapCheckStartedAt = DateTime.UtcNow;
        TransitionTo(TurnInState.CheckingOvercapDialog);
    }

    private void CheckOvercapDialog()
    {
        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            new AddonMaster.SelectYesno((IntPtr)addon).No();
            OvercapDetected = true;
            _shopAddon.CloseWindow();
            TransitionTo(TurnInState.Completed);
            return;
        }

        if ((DateTime.UtcNow - _overcapCheckStartedAt) > TimeSpan.FromMilliseconds(500))
            TransitionTo(TurnInState.WaitingForSubmit);
    }

    private void WaitForSubmit()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        var current = _turnInQueue.Peek();
        var currentCount = Context!.Inventory.GetCollectableInventoryItemCount(current.itemId);
        if (currentCount >= _inventoryCountBeforeSubmit)
        {
            if ((DateTime.UtcNow - _submitStartedAt) > _submitTimeout)
                Fail($"Collectable submission did not complete: {current.name}");

            return;
        }

        var newCount = current.count - 1;
        _turnInQueue.Dequeue();

        if (newCount > 0)
        {
            var remaining = _turnInQueue.ToList();
            _turnInQueue.Clear();
            _turnInQueue.Enqueue((current.itemId, current.name, newCount, current.jobId));
            foreach (var item in remaining)
                _turnInQueue.Enqueue(item);
        }
        else
        {
            _currentItemId = 0;
        }

        _inventoryCountBeforeSubmit = 0;
        _submitStartedAt = DateTime.MinValue;
        TransitionTo(TurnInState.CheckingForMore);
        LastActionAt = DateTime.UtcNow;
    }

    private void CheckForMore()
    {
        if ((DateTime.UtcNow - LastActionAt) < _actionDelay)
            return;

        TransitionTo(_turnInQueue.Count > 0 ? TurnInState.SelectingJob : TurnInState.Completed);
    }

    private void Complete()
    {
        _shopAddon.CloseWindow();
        Context?.Inventory.InvalidateTransientCaches();
        MarkCompleted();
        TransitionTo(TurnInState.Idle);
    }

    private void Fail(string message)
    {
        _shopAddon.CloseWindow();
        Context?.Inventory.InvalidateTransientCaches();
        FailJob(message);
        TransitionTo(TurnInState.Failed);
    }

    private void TransitionTo(TurnInState state)
    {
        _stateMachine.TransitionTo(state);
        TransitionedAt = _stateMachine.StateEnteredAt;
    }

    private CollectableShop GetPreferredShop()
        => Context!.Config.PreferredCollectableShop!;
}
