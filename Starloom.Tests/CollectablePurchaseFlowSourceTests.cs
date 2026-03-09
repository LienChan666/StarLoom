using Xunit;

namespace StarLoom.Tests;

public sealed class CollectablePurchaseFlowSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Configuration_Defaults_Post_Purchase_Action_And_Return_Point_To_Inn()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Configuration.cs"));

        Assert.Contains("PurchaseCompletionAction", source);
        Assert.Contains("PostPurchaseAction { get; set; } = PurchaseCompletionAction.ReturnToConfiguredPoint;", source);
        Assert.Contains("DefaultCraftReturnPoint { get; set; } = HousingReturnPoint.CreateInn();", source);
    }

    [Fact]
    public void HousingReturnPoint_And_LifestreamIpc_Support_Inn_Returns()
    {
        var returnPointSource = File.ReadAllText(Path.Combine(RepoRoot, "Data", "HousingReturnPoint.cs"));
        var serviceSource = File.ReadAllText(Path.Combine(RepoRoot, "Services", "HousingReturnPointService.cs"));
        var ipcSource = File.ReadAllText(Path.Combine(RepoRoot, "IPC", "LifestreamIPC.cs"));

        Assert.Contains("public bool IsInn { get; set; }", returnPointSource);
        Assert.Contains("CreateInn()", returnPointSource);
        Assert.Contains("result.Add(HousingReturnPoint.CreateInn())", serviceSource);
        Assert.Contains("TryResolveConfiguredPoint", serviceSource);
        Assert.Contains("point.IsInn", serviceSource);
        Assert.Contains("EnqueueInnShortcut", ipcSource);
    }

    [Fact]
    public void WorkflowBuilder_Appends_Post_Purchase_Action_After_Scrip_Purchase()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Workflows", "WorkflowBuilder.cs"));

        Assert.Contains("BuildPostPurchaseActionJob()", source);
        Assert.Contains("new ScripPurchaseJob()", source);
        Assert.Contains("BuildPostPurchaseActionJob(),", source);
        Assert.Contains("jobs.Add(BuildPostPurchaseActionJob());", source);
    }

    [Fact]
    public void WorkflowBuilder_Only_Runs_Configured_Purchases_For_Pending_Items()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Workflows", "WorkflowBuilder.cs"));

        Assert.Contains("ShouldRunConfiguredPurchaseWorkflow()", source);
        Assert.Contains("PendingPurchaseResolver", source);
        Assert.Contains("_config.BuyAfterEachTurnIn && _pendingPurchaseResolver.Resolve().Count > 0", source);
        Assert.DoesNotContain("GetPendingPurchaseItems()", source);
    }

    [Fact]
    public void WorkflowStartValidator_Blocks_Empty_Or_Already_Completed_Purchase_List()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Workflows", "WorkflowStartValidator.cs"));

        Assert.Contains("The purchase list is empty.", source);
        Assert.Contains("The purchase list is empty or has no valid target quantities.", source);
        Assert.Contains("All configured purchase items already reached their target quantities.", source);
        Assert.Contains("PendingPurchaseResolver", source);
        Assert.Contains("_pendingPurchaseResolver.Resolve()", source);
    }

    [Fact]
    public void WorkflowBuilder_Always_Appends_Post_Processing_Action_For_Configured_Workflow()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Workflows", "WorkflowBuilder.cs"));

        Assert.Contains("public IReadOnlyList<IAutomationJob> CreateConfiguredWorkflow()", source);
        Assert.Contains("jobs.Add(new ScripPurchaseJob());", source);
        Assert.Contains("jobs.Add(BuildPostPurchaseActionJob());", source);
        Assert.True(
            source.LastIndexOf("jobs.Add(BuildPostPurchaseActionJob());", StringComparison.Ordinal) >
            source.IndexOf("if (ShouldRunConfiguredPurchaseWorkflow())", StringComparison.Ordinal),
            "Configured workflow should always append its post-processing action after optional purchase handling.");
    }

    [Fact]
    public void WorkflowStartValidator_CanValidate_Artisan_List_Start_Position()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Workflows", "WorkflowStartValidator.cs"));

        Assert.Contains("CanStartArtisanList", source);
        Assert.Contains("HousingReturnPointService.TryResolveConfiguredPoint", source);
        Assert.Contains("HousingReturnPointService.IsInsideInn()", source);
        Assert.Contains("HousingReturnPointService.IsInsideHouse()", source);
        Assert.Contains("Svc.ClientState.TerritoryType", source);
    }

    [Fact]
    public void ManagedArtisanSession_Validates_Position_Before_Starting_Artisan_List()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "ManagedArtisanSession.cs"));

        Assert.Contains("_workflowValidator.CanStartArtisanList(out var errorMessage)", source);
        Assert.Contains("SetFailure(errorMessage, stopArtisan: false);", source);
    }

    [Fact]
    public void Return_And_Close_Game_Jobs_Handle_Inn_And_Process_Termination()
    {
        var returnSource = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ReturnToCraftPointJob.cs"));
        var closeSource = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "CloseGameJob.cs"));

        Assert.Contains("_target!.IsInn", returnSource);
        Assert.Contains("LifestreamIPC.EnqueueInnShortcut", returnSource);
        Assert.Contains("WaitingForInn", returnSource);
        Assert.Contains("Process.GetCurrentProcess().Kill();", closeSource);
        Assert.Contains("public string Id => \"close-game\";", closeSource);
    }

    [Fact]
    public void ReturnToCraftPointJob_Completes_Inn_Return_As_Soon_As_Player_Is_Inside_Inn()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ReturnToCraftPointJob.cs"));

        Assert.Contains("private void WaitForInn()", source);
        Assert.Contains("if (HousingReturnPointService.IsInsideInn())", source);
        Assert.Contains("if (IsTransitioning() || LifestreamIPC.IsBusy())", source);
        Assert.True(
            source.IndexOf("if (HousingReturnPointService.IsInsideInn())", StringComparison.Ordinal) <
            source.IndexOf("if (IsTransitioning() || LifestreamIPC.IsBusy())", StringComparison.Ordinal),
            "WaitForInn should check inn arrival before deferring on Lifestream busy state.");
    }

    [Fact]
    public void ReturnToCraftPointJob_Uses_Five_Minute_Timeout_For_Inn_Return()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ReturnToCraftPointJob.cs"));

        Assert.Contains("TimeSpan.FromMinutes(5)", source);
    }

    [Fact]
    public void ScripShopAddon_Uses_Cached_Index_For_Item_Selection()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Addons", "ScripShopAddon.cs"));

        Assert.Contains("shopItem.Index", source);
        Assert.DoesNotContain("Svc.Log.", source);
        Assert.DoesNotContain("TryFindVisibleItemIndex", source);
    }

    [Fact]
    public void ScripPurchaseJob_Verifies_Target_Inventory_Count_Before_Completing_Purchase()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ScripPurchaseJob.cs"));

        Assert.Contains("_purchaseStartedAt", source);
        Assert.Contains("_currentTargetItemId", source);
        Assert.Contains("_inventoryCountBeforePurchase", source);
        Assert.Contains("Context!.Inventory.GetInventoryItemCount(_currentTargetItemId)", source);
        Assert.Contains("WaitForPurchaseComplete", source);
        Assert.DoesNotContain("var next = _purchaseQueue.Dequeue();", source);
    }

    [Fact]
    public void ManagedSession_Restarts_Workflow_When_Collectables_Remain_After_Threshold_Run()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "ManagedArtisanSession.cs"));

        Assert.Contains("_orchestrator.State == OrchestratorState.Completed", source);
        Assert.Contains("_inventory.HasCollectableTurnIns()", source);
        Assert.Contains("_orchestrator.TryStart(_jobFactory())", source);
        Assert.Contains("IsBelowFreeSlotThreshold()", source);
    }

    [Fact]
    public void ReturnToCraftPointJob_CanStart_No_Longer_Depends_On_Free_Slot_Threshold()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ReturnToCraftPointJob.cs"));

        Assert.Contains("public override bool CanStart()", source);
        Assert.Contains("=> true;", source);
        Assert.DoesNotContain("Plugin.P.Config.FreeSlotThreshold", source);
        Assert.DoesNotContain("InventoryService.HasCollectableTurnIns()", source);
    }

    [Fact]
    public void ScripPurchaseJob_Does_Not_Silently_Skip_Items_When_No_Purchasable_Amount()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ScripPurchaseJob.cs"));

        Assert.Contains("scrips < 0", source);
        Assert.Contains("Fail($\"Could not read scrip count for", source);
        Assert.Contains("Fail($\"Not enough scrips to purchase one item", source);
    }

    [Fact]
    public void InventoryService_Uses_Tomestone_Count_For_Currency_Items()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "InventoryService.cs"));

        Assert.Contains("GetTomestoneCount", source);
        Assert.Contains("GetCurrencyItemCount", source);
    }

    [Fact]
    public void ScripPurchaseJob_Uses_Configured_Currency_Item_Count_When_Selecting_Purchase_Amount()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ScripPurchaseJob.cs"));

        Assert.Contains("CurrencyItemId", source);
        Assert.Contains("Context!.Inventory.GetCurrencyItemCount(next.CurrencyItemId)", source);
        Assert.DoesNotContain("_shopAddon.GetScripCount()", source);
    }

    [Fact]
    public void CollectableTurnInJob_Fails_When_Collectable_Shop_Window_Does_Not_Open()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "CollectableTurnInJob.cs"));

        Assert.Contains("Timed out while waiting for the collectable window.", source);
        Assert.Contains("TransitionedAt", source);
        Assert.Contains("UpdateShopWindow(", source);
    }

    [Fact]
    public void CollectableTurnInJob_Confirms_Inventory_Changed_Before_Dequeuing_Submit_Queue()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "CollectableTurnInJob.cs"));

        Assert.Contains("GetCollectableInventoryItemCount", source);
        Assert.Contains("_inventoryCountBeforeSubmit", source);
        Assert.Contains("_submitStartedAt", source);
        Assert.Contains("if (currentCount >= _inventoryCountBeforeSubmit)", source);
    }

    [Fact]
    public void ScripPurchaseJob_Fails_When_Scrip_Shop_Window_Does_Not_Open()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ScripPurchaseJob.cs"));

        Assert.Contains("Timed out while waiting for the scrip shop window.", source);
        Assert.Contains("TransitionedAt", source);
        Assert.Contains("UpdateShopWindow(", source);
    }

    [Fact]
    public void ScripPurchaseJob_Verifies_Purchase_Dialog_Target_Name_Before_Confirming()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Jobs", "ScripPurchaseJob.cs"));

        Assert.Contains("PurchaseItem(_currentTargetItemId, _currentTargetItemName)", source);
        Assert.Contains("PurchaseDialogResult.MismatchedItem", source);
    }

    [Fact]
    public void ScripShopAddon_Verifies_Purchase_Dialog_Using_AtkValues_Instead_Of_Window_Text_Fallback()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Addons", "ScripShopAddon.cs"));

        Assert.Contains("addon->AtkValuesSpan", source);
        Assert.Contains("value.UInt == expectedItemId", source);
        Assert.DoesNotContain("ContainsExpectedText(visibleTexts, expectedItemName)", source);
    }

    [Fact]
    public void ScripShopCatalogBuilder_Uses_SpecialShop_Order_For_Display_Index()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "ScripShopCatalogBuilder.cs"));

        Assert.Contains("entry.Order", source);
        Assert.Contains("OrderBy(candidate => candidate.SortOrder)", source);
        Assert.Contains("Index = candidate.DisplayIndex", source);
    }

    [Fact]
    public void ScripShopAddon_Removes_Unused_Window_Scrip_Count_Parsing()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Addons", "ScripShopAddon.cs"));

        Assert.DoesNotContain("public int GetScripCount()", source);
        Assert.DoesNotContain("TryParseDisplayedCount", source);
    }

}


