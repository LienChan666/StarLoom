using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using Starloom.Addons;
using Starloom.Core;
using Starloom.Data;
using Starloom.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Starloom.Jobs;

public sealed unsafe class CollectableTurnInJob : IAutomationJob
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

    private JobContext? _context;
    private TurnInState _state = TurnInState.Idle;
    private uint _currentItemId;
    private int _currentJobId = -1;
    private DateTime _lastAction = DateTime.MinValue;
    private DateTime _stateEnteredAt = DateTime.MinValue;
    private DateTime _overcapCheckStartedAt = DateTime.MinValue;
    private DateTime _submitStartedAt = DateTime.MinValue;
    private bool _navigationStarted;
    private int _inventoryCountBeforeSubmit;

    public string Id => "collectable-turn-in";
    public string StatusText { get; private set; } = "空闲";
    public JobStatus Status { get; private set; } = JobStatus.Idle;
    public bool OvercapDetected { get; private set; }

    public bool CanStart() => InventoryService.HasCollectableTurnIns();

    public void Start(JobContext context)
    {
        _context = context;
        ResetRunState();
        InventoryService.InvalidateTransientCaches();
        if (context.Config.PreferredCollectableShop == null)
        {
            Status = JobStatus.Failed;
            StatusText = "未配置收藏品商店。";
            TransitionTo(TurnInState.Failed);
            return;
        }

        Status = JobStatus.Running;
        StatusText = "正在扫描收藏品";
        TransitionTo(TurnInState.CheckingInventory);
    }

    public void Update()
    {
        if (Status != JobStatus.Running || _context == null)
            return;

        try
        {
            switch (_state)
            {
                case TurnInState.CheckingInventory:
                    CheckInventory();
                    break;
                case TurnInState.MovingToShop:
                    MoveToShop();
                    break;
                case TurnInState.WaitingForShopWindow:
                    WaitForShopWindow();
                    break;
                case TurnInState.SelectingJob:
                    SelectJob();
                    break;
                case TurnInState.SelectingItem:
                    SelectItem();
                    break;
                case TurnInState.SubmittingItem:
                    SubmitItem();
                    break;
                case TurnInState.CheckingOvercapDialog:
                    CheckOvercapDialog();
                    break;
                case TurnInState.WaitingForSubmit:
                    WaitForSubmit();
                    break;
                case TurnInState.CheckingForMore:
                    CheckForMore();
                    break;
                case TurnInState.Completed:
                    Complete();
                    break;
                case TurnInState.Failed:
                    Fail(StatusText);
                    break;
            }
        }
        catch (Exception ex)
        {
            Fail($"收藏品提交异常：{ex.Message}");
        }
    }

    public void Stop()
    {
        _context?.Navigation.Stop();
        _shopAddon.CloseWindow();
        ResetRunState();
        Status = JobStatus.Idle;
        StatusText = "已停止";
    }

    private void ResetRunState()
    {
        _turnInQueue.Clear();
        _currentItemId = 0;
        _currentJobId = -1;
        _lastAction = DateTime.MinValue;
        _stateEnteredAt = DateTime.MinValue;
        _overcapCheckStartedAt = DateTime.MinValue;
        _submitStartedAt = DateTime.MinValue;
        _navigationStarted = false;
        _inventoryCountBeforeSubmit = 0;
        OvercapDetected = false;
        TransitionTo(TurnInState.Idle);
    }

    private void CheckInventory()
    {
        StatusText = "正在扫描收藏品";
        var collectables = InventoryService.GetCurrentInventoryItems()
            .Where(item => item.IsCollectable && InventoryService.IsCollectableTurnInItem(item.BaseItemId))
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
        if (!_navigationStarted)
        {
            StatusText = "正在前往收藏品 NPC";
            _context!.Navigation.NavigateTo(new NavigationTarget(
                shop.Location,
                shop.AetheryteId,
                shop.TerritoryId,
                2f,
                shop.IsLifestreamRequired,
                shop.LifestreamCommand));
            _navigationStarted = true;
            return;
        }

        if (_context!.Navigation.State == NavigationService.NavigationState.Arrived)
        {
            _navigationStarted = false;
            TransitionTo(TurnInState.WaitingForShopWindow);
            _lastAction = DateTime.MinValue;
            return;
        }

        if (_context.Navigation.State == NavigationService.NavigationState.Failed)
            Fail(_context.Navigation.ErrorMessage ?? "无法到达收藏品 NPC");
    }

    private void WaitForShopWindow()
    {
        StatusText = "正在打开收藏品窗口";
        if (_shopAddon.IsReady)
        {
            TransitionTo(TurnInState.SelectingJob);
            _lastAction = DateTime.MinValue;
            return;
        }

        if ((DateTime.UtcNow - _stateEnteredAt) > _shopWindowTimeout)
        {
            Fail("等待收藏品窗口超时。");
            return;
        }

        if ((DateTime.UtcNow - _lastAction) < TimeSpan.FromSeconds(1))
            return;

        if (_context!.NpcInteraction.TryInteract(GetPreferredShop().NpcId))
            _lastAction = DateTime.UtcNow;
    }

    private void SelectJob()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        StatusText = "正在选择收藏品职业页签";
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
            _lastAction = DateTime.UtcNow;
        }

        TransitionTo(TurnInState.SelectingItem);
    }

    private void SelectItem()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        StatusText = "正在选择收藏品条目";
        var next = _turnInQueue.Peek();
        if (_currentItemId != next.itemId)
        {
            _shopAddon.SelectItemById(next.itemId);
            _currentItemId = next.itemId;
            _lastAction = DateTime.UtcNow;
            return;
        }

        TransitionTo(TurnInState.SubmittingItem);
    }

    private void SubmitItem()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        StatusText = "正在提交收藏品";
        InventoryService.InvalidateTransientCaches();
        _inventoryCountBeforeSubmit = InventoryService.GetCollectableInventoryItemCount(_currentItemId);
        _submitStartedAt = DateTime.UtcNow;
        _shopAddon.SubmitItem();
        _lastAction = DateTime.UtcNow;
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
            StatusText = "检测到工票溢出";
            TransitionTo(TurnInState.Completed);
            return;
        }

        if ((DateTime.UtcNow - _overcapCheckStartedAt) > TimeSpan.FromMilliseconds(500))
            TransitionTo(TurnInState.WaitingForSubmit);
    }

    private void WaitForSubmit()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        var current = _turnInQueue.Peek();
        var currentCount = InventoryService.GetCollectableInventoryItemCount(current.itemId);
        if (currentCount >= _inventoryCountBeforeSubmit)
        {
            if ((DateTime.UtcNow - _submitStartedAt) > _submitTimeout)
                Fail($"收藏品提交未生效：{current.name}");

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
        _lastAction = DateTime.UtcNow;
    }

    private void CheckForMore()
    {
        if ((DateTime.UtcNow - _lastAction) < _actionDelay)
            return;

        TransitionTo(_turnInQueue.Count > 0 ? TurnInState.SelectingJob : TurnInState.Completed);
    }

    private void Complete()
    {
        _context?.Navigation.Stop();
        _shopAddon.CloseWindow();
        InventoryService.InvalidateTransientCaches();
        Status = JobStatus.Completed;
        StatusText = OvercapDetected ? "收藏品提交完成（检测到工票溢出）" : "收藏品提交完成";
        TransitionTo(TurnInState.Idle);
    }

    private void Fail(string message)
    {
        _context?.Navigation.Stop();
        _shopAddon.CloseWindow();
        InventoryService.InvalidateTransientCaches();
        Status = JobStatus.Failed;
        StatusText = message;
        TransitionTo(TurnInState.Failed);
    }

    private void TransitionTo(TurnInState state)
    {
        _state = state;
        _stateEnteredAt = DateTime.UtcNow;
    }

    private CollectableShop GetPreferredShop()
        => _context!.Config.PreferredCollectableShop!;
}
