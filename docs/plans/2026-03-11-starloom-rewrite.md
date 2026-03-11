# StarLoom Rewrite Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a clean-room rewrite of `StarLoom` in an empty worktree with a single plugin project, a separate test project, one top-level workflow state machine, IPC-driven artisan/navigation control, and TaskManager-driven local turn-in/purchase flows.

**Architecture:** The rewrite keeps one concrete plugin entry at the repository root and organizes behavior by direct business folders: `Config`, `Game`, `Ipc`, `Tasks`, and `Ui`. `Tasks/WorkflowTask.cs` owns the only explicit state machine, `Tasks/Artisan/ArtisanTask.cs` and `Tasks/Navigation/NavigationTask.cs` poll IPC state without using `TaskManager`, and `Tasks/TurnIn/TurnInTask.cs` plus `Tasks/Purchase/PurchaseTask.cs` use `TaskManager` only for local in-process steps.

**Tech Stack:** C# 14 / .NET 10, Dalamud, ECommons, xUnit

---

### Task 1: Bootstrap The Empty Repository

**Files:**
- Create: `StarLoom.csproj`
- Create: `StarLoom.sln`
- Create: `StarLoom.cs`
- Create: `GlobalUsings.cs`
- Create: `StarLoom.Tests/StarLoom.Tests.csproj`
- Create: `StarLoom.Tests/Bootstrap/ProjectSmokeTests.cs`

**Step 1: Create the solution, project files, and a failing smoke test**

Create the root project and test project with a failing reference to `PluginConfig`:

```csharp
using Xunit;

namespace StarLoom.Tests.Bootstrap;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void PluginConfig_Should_Exist()
    {
        _ = typeof(PluginConfig);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ProjectSmokeTests`
Expected: FAIL with `CS0246` because `PluginConfig` does not exist yet.

**Step 3: Create the minimal root project shell**

Create:

- `StarLoom.csproj` with Dalamud and ECommons references, `net10.0-windows7.0`, nullable enabled, unsafe enabled, and output paths aligned with the old plugin project.
- `StarLoom.sln` containing the plugin project and test project.
- `GlobalUsings.cs` with shared `using` directives for Dalamud services and logging.
- `StarLoom.cs` with a minimal `IDalamudPlugin` implementation that initializes ECommons, stores constructor dependencies as concrete fields, and wires `/starloom` plus framework update callbacks.

Create a temporary minimal `Config/PluginConfig.cs` shell containing:

```csharp
namespace StarLoom.Config;

public sealed class PluginConfig
{
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ProjectSmokeTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add StarLoom.csproj StarLoom.sln StarLoom.cs GlobalUsings.cs Config/PluginConfig.cs StarLoom.Tests/StarLoom.Tests.csproj StarLoom.Tests/Bootstrap/ProjectSmokeTests.cs
git commit -m "chore: bootstrap empty StarLoom rewrite"
```

### Task 2: Build Config And Localization

**Files:**
- Modify: `Config/PluginConfig.cs`
- Create: `Config/ConfigStore.cs`
- Create: `Config/ConfigDefaults.cs`
- Create: `Config/PostPurchaseAction.cs`
- Create: `Config/CollectableShopConfig.cs`
- Create: `Config/ReturnPointConfig.cs`
- Create: `Resources/Localization/en.json`
- Create: `Resources/Localization/zh.json`
- Create: `StarLoom.Tests/Config/ConfigDefaultsTests.cs`

**Step 1: Write the failing config test**

```csharp
using StarLoom.Config;
using Xunit;

namespace StarLoom.Tests.Config;

public sealed class ConfigDefaultsTests
{
    [Fact]
    public void Apply_Should_Set_Default_Return_Point_And_Language()
    {
        var config = new PluginConfig();

        ConfigDefaults.Apply(config);

        Assert.NotNull(config.defaultReturnPoint);
        Assert.Equal("zh", config.uiLanguage);
        Assert.Equal(PostPurchaseAction.ReturnToConfiguredPoint, config.postPurchaseAction);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ConfigDefaultsTests`
Expected: FAIL because `ConfigDefaults`, `PostPurchaseAction`, and config fields do not exist yet.

**Step 3: Write the minimal config implementation**

Implement `PluginConfig` as a concrete data class with direct fields:

```csharp
public sealed class PluginConfig
{
    public int version { get; set; } = 1;
    public int artisanListId { get; set; }
    public ReturnPointConfig? defaultReturnPoint { get; set; }
    public CollectableShopConfig? preferredCollectableShop { get; set; }
    public PostPurchaseAction postPurchaseAction { get; set; }
    public bool showStatusOverlay { get; set; }
    public string uiLanguage { get; set; } = string.Empty;
    public int reserveScripAmount { get; set; }
    public int freeSlotThreshold { get; set; }
    public List<PurchaseItemConfig> scripShopItems { get; set; } = [];
}
```

Implement `ConfigDefaults.Apply` to populate null or empty values, `ConfigStore` to load and save via `IDalamudPluginInterface`, and create the first `en.json` / `zh.json` resource files with keys for home, settings, workflow state, and failure messages.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ConfigDefaultsTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Config/PluginConfig.cs Config/ConfigStore.cs Config/ConfigDefaults.cs Config/PostPurchaseAction.cs Config/CollectableShopConfig.cs Config/ReturnPointConfig.cs Resources/Localization/en.json Resources/Localization/zh.json StarLoom.Tests/Config/ConfigDefaultsTests.cs
git commit -m "feat: add rewrite config foundation"
```

### Task 3: Define IPC Contracts And Concrete IPC Adapters

**Files:**
- Create: `Ipc/IArtisanIpc.cs`
- Create: `Ipc/IVNavmeshIpc.cs`
- Create: `Ipc/ILifestreamIpc.cs`
- Create: `Ipc/ArtisanIpc.cs`
- Create: `Ipc/VNavmeshIpc.cs`
- Create: `Ipc/LifestreamIpc.cs`
- Create: `Tasks/Artisan/ArtisanSnapshot.cs`
- Create: `StarLoom.Tests/Tasks/Artisan/ArtisanSnapshotTests.cs`

**Step 1: Write the failing artisan snapshot test**

```csharp
using StarLoom.Tasks.Artisan;
using Xunit;

namespace StarLoom.Tests.Tasks.Artisan;

public sealed class ArtisanSnapshotTests
{
    [Fact]
    public void IsReady_Should_Be_True_When_Ipc_Is_Available_And_List_Id_Is_Valid()
    {
        var snapshot = new ArtisanSnapshot(true, true, false, false, false, 7);

        Assert.True(snapshot.isReady);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ArtisanSnapshotTests`
Expected: FAIL because `ArtisanSnapshot` does not exist yet.

**Step 3: Write the minimal IPC layer**

Create the three IPC interfaces with direct methods for availability, status reads, and commands. Create thin concrete IPC classes that wrap Dalamud IPC subscribers and produce technical logs through `Svc.Log`.

Create `ArtisanSnapshot` as a compact immutable record:

```csharp
namespace StarLoom.Tasks.Artisan;

public readonly record struct ArtisanSnapshot(
    bool isAvailable,
    bool isListRunning,
    bool isPaused,
    bool hasStopRequest,
    bool isBusy,
    int listId)
{
    public bool isReady => isAvailable && listId > 0;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ArtisanSnapshotTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ipc/IArtisanIpc.cs Ipc/IVNavmeshIpc.cs Ipc/ILifestreamIpc.cs Ipc/ArtisanIpc.cs Ipc/VNavmeshIpc.cs Ipc/LifestreamIpc.cs Tasks/Artisan/ArtisanSnapshot.cs StarLoom.Tests/Tasks/Artisan/ArtisanSnapshotTests.cs
git commit -m "feat: add ipc contracts and artisan snapshot"
```

### Task 4: Build Direct Game Adapters

**Files:**
- Create: `Game/InventoryGame.cs`
- Create: `Game/NpcGame.cs`
- Create: `Game/PlayerStateGame.cs`
- Create: `Game/LocationGame.cs`
- Create: `Game/CollectableShopGame.cs`
- Create: `Game/ScripShopGame.cs`
- Create: `StarLoom.Tests/Game/InventorySelectionTests.cs`

**Step 1: Write the failing inventory selection test**

```csharp
using StarLoom.Game;
using Xunit;

namespace StarLoom.Tests.Game;

public sealed class InventorySelectionTests
{
    [Fact]
    public void Collectables_Should_Group_By_Item_Id()
    {
        var items = new[]
        {
            new InventoryItemView(1001, true, 2),
            new InventoryItemView(1001, true, 1),
            new InventoryItemView(1002, false, 4),
        };

        var grouped = InventoryGame.GroupCollectables(items);

        Assert.Single(grouped);
        Assert.Equal(3, grouped[0].quantity);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~InventorySelectionTests`
Expected: FAIL because `InventoryGame` and `InventoryItemView` do not exist yet.

**Step 3: Write the minimal game layer**

Create direct, concrete adapters for inventory, NPC interaction, player state, location checks, collectable window operations, and scrip shop window operations. Keep them free of workflow decisions.

Implement the first pure helper in `InventoryGame`:

```csharp
public static List<CollectableGroup> GroupCollectables(IEnumerable<InventoryItemView> items)
{
    return items
        .Where(item => item.isCollectable)
        .GroupBy(item => item.itemId)
        .Select(group => new CollectableGroup(group.Key, group.Sum(item => item.quantity)))
        .ToList();
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~InventorySelectionTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Game/InventoryGame.cs Game/NpcGame.cs Game/PlayerStateGame.cs Game/LocationGame.cs Game/CollectableShopGame.cs Game/ScripShopGame.cs StarLoom.Tests/Game/InventorySelectionTests.cs
git commit -m "feat: add direct game adapters"
```

### Task 5: Implement `NavigationTask` Without TaskManager

**Files:**
- Create: `Tasks/Navigation/NavigationTask.cs`
- Create: `Tasks/Navigation/NavigationPlan.cs`
- Create: `Tasks/Navigation/NavigationModels.cs`
- Create: `StarLoom.Tests/Tasks/Navigation/NavigationTaskTests.cs`

**Step 1: Write the failing navigation task test**

```csharp
using StarLoom.Tasks.Navigation;
using Xunit;

namespace StarLoom.Tests.Tasks.Navigation;

public sealed class NavigationTaskTests
{
    [Fact]
    public void Update_Should_Complete_When_External_Navigation_Reports_Arrival()
    {
        var navigationTask = NavigationTask.CreateForTests();
        navigationTask.Start(new NavigationRequest(1, "collectable"));
        navigationTask.SetTestState(isRunning: true, isArrived: true, hasFailed: false);

        navigationTask.Update();

        Assert.True(navigationTask.isCompleted);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~NavigationTaskTests`
Expected: FAIL because `NavigationTask` and `NavigationRequest` do not exist yet.

**Step 3: Write the minimal navigation implementation**

`NavigationTask` should be a concrete stateful class that:

- accepts a `NavigationRequest`
- triggers IPC-backed movement through `VNavmeshIpc` and optional `LifestreamIpc`
- exposes `isRunning`, `isCompleted`, `hasFailed`, and `errorMessage`
- advances only through `Start`, `Update`, and `Stop`

Create the request model:

```csharp
public readonly record struct NavigationRequest(uint territoryId, string reason);
```

Create a `CreateForTests()` factory or internal constructor to avoid unnecessary extra abstractions while still making state transition tests possible.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~NavigationTaskTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Tasks/Navigation/NavigationTask.cs Tasks/Navigation/NavigationPlan.cs Tasks/Navigation/NavigationModels.cs StarLoom.Tests/Tasks/Navigation/NavigationTaskTests.cs
git commit -m "feat: add ipc-driven navigation task"
```

### Task 6: Implement `ArtisanTask` Without TaskManager

**Files:**
- Create: `Tasks/Artisan/ArtisanTask.cs`
- Create: `StarLoom.Tests/Tasks/Artisan/ArtisanTaskTests.cs`

**Step 1: Write the failing artisan task test**

```csharp
using StarLoom.Tasks.Artisan;
using Xunit;

namespace StarLoom.Tests.Tasks.Artisan;

public sealed class ArtisanTaskTests
{
    [Fact]
    public void Pause_Should_Request_Pause_When_List_Is_Running()
    {
        var artisanTask = ArtisanTask.CreateForTests();
        artisanTask.SetTestSnapshot(new ArtisanSnapshot(true, true, false, false, true, 7));

        artisanTask.Pause();

        Assert.True(artisanTask.lastPauseRequested);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ArtisanTaskTests`
Expected: FAIL because `ArtisanTask` does not exist yet.

**Step 3: Write the minimal artisan control implementation**

`ArtisanTask` should expose:

- `CanControl(out string errorMessage)`
- `StartConfiguredList()`
- `Pause()`
- `Resume()`
- `Stop()`
- `Update()`
- `GetSnapshot()`

It should depend only on `IArtisanIpc` plus config and should surface user-facing failures through `DuoLog` only when the caller cannot continue.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~ArtisanTaskTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Tasks/Artisan/ArtisanTask.cs StarLoom.Tests/Tasks/Artisan/ArtisanTaskTests.cs
git commit -m "feat: add artisan control task"
```

### Task 7: Implement `TurnInTask` With TaskManager

**Files:**
- Create: `Tasks/TurnIn/TurnInTask.cs`
- Create: `Tasks/TurnIn/TurnInPlan.cs`
- Create: `Tasks/TurnIn/TurnInModels.cs`
- Create: `StarLoom.Tests/Tasks/TurnIn/TurnInPlanTests.cs`

**Step 1: Write the failing turn-in plan test**

```csharp
using StarLoom.Tasks.TurnIn;
using Xunit;

namespace StarLoom.Tests.Tasks.TurnIn;

public sealed class TurnInPlanTests
{
    [Fact]
    public void BuildQueue_Should_Ignore_NonCollectables()
    {
        var queue = TurnInPlan.BuildQueue(
            [new TurnInCandidate(1001, "A", 2, true), new TurnInCandidate(1002, "B", 3, false)]);

        Assert.Single(queue);
        Assert.Equal(1001u, queue[0].itemId);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~TurnInPlanTests`
Expected: FAIL because `TurnInPlan` and its models do not exist yet.

**Step 3: Write the minimal turn-in implementation**

`TurnInPlan` should return a compact queue of collectable items. `TurnInTask` should own one `TaskManager` instance and enqueue only local steps such as:

- build queue
- open collectable window
- select job
- select item
- submit
- cleanup

Do not call IPC from inside `TurnInTask`.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~TurnInPlanTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Tasks/TurnIn/TurnInTask.cs Tasks/TurnIn/TurnInPlan.cs Tasks/TurnIn/TurnInModels.cs StarLoom.Tests/Tasks/TurnIn/TurnInPlanTests.cs
git commit -m "feat: add local turn-in task"
```

### Task 8: Implement `PurchaseTask` With TaskManager

**Files:**
- Create: `Tasks/Purchase/PurchaseTask.cs`
- Create: `Tasks/Purchase/PurchasePlan.cs`
- Create: `Tasks/Purchase/PurchaseCatalog.cs`
- Create: `Tasks/Purchase/PurchaseModels.cs`
- Create: `StarLoom.Tests/Tasks/Purchase/PurchasePlanTests.cs`

**Step 1: Write the failing purchase plan test**

```csharp
using StarLoom.Tasks.Purchase;
using Xunit;

namespace StarLoom.Tests.Tasks.Purchase;

public sealed class PurchasePlanTests
{
    [Fact]
    public void BuildQueue_Should_Respect_Reserve_Amount()
    {
        var queue = PurchasePlan.BuildQueue(
            [new PurchaseTarget(2001, "Cordial", 20, 10, 999)],
            currentScrips: 205,
            reserveAmount: 200);

        Assert.Empty(queue);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~PurchasePlanTests`
Expected: FAIL because `PurchasePlan` and `PurchaseTarget` do not exist yet.

**Step 3: Write the minimal purchase implementation**

`PurchasePlan` should compute a purchase queue from config, inventory counts, and reserve rules. `PurchaseCatalog` should store static shop metadata. `PurchaseTask` should use `TaskManager` only for local steps:

- prepare queue
- open shop
- select page
- select item
- confirm purchase
- cleanup

Do not start navigation or call IPC from inside `PurchaseTask`.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~PurchasePlanTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Tasks/Purchase/PurchaseTask.cs Tasks/Purchase/PurchasePlan.cs Tasks/Purchase/PurchaseCatalog.cs Tasks/Purchase/PurchaseModels.cs StarLoom.Tests/Tasks/Purchase/PurchasePlanTests.cs
git commit -m "feat: add local purchase task"
```

### Task 9: Implement The Single Workflow State Machine

**Files:**
- Create: `Tasks/WorkflowTask.cs`
- Create: `StarLoom.Tests/Tasks/WorkflowTaskTests.cs`

**Step 1: Write the failing workflow test**

```csharp
using StarLoom.Tasks;
using Xunit;

namespace StarLoom.Tests.Tasks;

public sealed class WorkflowTaskTests
{
    [Fact]
    public void Update_Should_Switch_To_TurnIn_When_Artisan_Is_Paused_And_Bag_Is_Full()
    {
        var workflowTask = WorkflowTask.CreateForTests();
        workflowTask.StartConfiguredWorkflow();
        workflowTask.SetTestSnapshot(isBusy: true, shouldTakeOver: true, artisanPaused: true);

        workflowTask.Update();

        Assert.Equal("TurnIn", workflowTask.currentStage);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~WorkflowTaskTests`
Expected: FAIL because `WorkflowTask` does not exist yet.

**Step 3: Write the minimal workflow implementation**

Create `Tasks/WorkflowTask.cs` as the only explicit state machine in the rewrite. Keep the enum inside this file. It should expose:

- `StartConfiguredWorkflow()`
- `StartTurnInOnly()`
- `StartPurchaseOnly()`
- `Stop()`
- `Update()`
- `GetStateText()`
- `isBusy`

It should coordinate the other task modules but never directly perform shop or IPC details itself.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~WorkflowTaskTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Tasks/WorkflowTask.cs StarLoom.Tests/Tasks/WorkflowTaskTests.cs
git commit -m "feat: add single workflow state machine"
```

### Task 10: Connect The UI To The New Workflow

**Files:**
- Create: `Ui/PluginUi.cs`
- Create: `Ui/MainWindow.cs`
- Create: `Ui/StatusOverlay.cs`
- Create: `Ui/Pages/HomePage.cs`
- Create: `Ui/Pages/SettingsPage.cs`
- Modify: `StarLoom.cs`
- Create: `StarLoom.Tests/Ui/HomePageStateTests.cs`

**Step 1: Write the failing UI state test**

```csharp
using StarLoom.Ui.Pages;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class HomePageStateTests
{
    [Fact]
    public void Start_Button_Should_Disable_When_Workflow_Is_Busy()
    {
        var state = HomePageState.FromWorkflow(isBusy: true, hasConfiguredPurchases: true);

        Assert.False(state.canStartConfiguredWorkflow);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~HomePageStateTests`
Expected: FAIL because `HomePageState` does not exist yet.

**Step 3: Write the minimal UI integration**

Create lightweight UI classes that only consume `WorkflowTask` and `ConfigStore`. Use a small view-state mapper such as:

```csharp
public readonly record struct HomePageState(bool canStartConfiguredWorkflow, bool canStop);
```

Wire `StarLoom.cs` so the plugin entry creates config, game, IPC, task, and UI objects explicitly in constructor order with no global static locator.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~HomePageStateTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ui/PluginUi.cs Ui/MainWindow.cs Ui/StatusOverlay.cs Ui/Pages/HomePage.cs Ui/Pages/SettingsPage.cs StarLoom.cs StarLoom.Tests/Ui/HomePageStateTests.cs
git commit -m "feat: connect ui to workflow task"
```

### Task 11: Run Full Verification And Close Gaps

**Files:**
- Modify: `StarLoom.cs`
- Modify: `Tasks/WorkflowTask.cs`
- Modify: `Tasks/Artisan/ArtisanTask.cs`
- Modify: `Tasks/Navigation/NavigationTask.cs`
- Modify: `Tasks/TurnIn/TurnInTask.cs`
- Modify: `Tasks/Purchase/PurchaseTask.cs`
- Modify: `StarLoom.Tests/Tasks/WorkflowTaskTests.cs`
- Modify: `StarLoom.Tests/Tasks/Navigation/NavigationTaskTests.cs`
- Modify: `StarLoom.Tests/Tasks/Artisan/ArtisanTaskTests.cs`

**Step 1: Add the final end-to-end behavior tests**

Add tests that verify:

- workflow enters turn-in when artisan should be taken over
- workflow chains navigation before turn-in and purchase
- purchase completion returns to the configured navigation target
- artisan resumes after a successful turn-in / purchase cycle
- missing IPC availability yields a user-facing error path

**Step 2: Run tests to verify they fail**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal`
Expected: FAIL until the workflow chaining and error handling are fully implemented.

**Step 3: Fill the missing workflow glue and failure handling**

Complete the remaining integration logic, ensure all user-actionable failures use `DuoLog`, and make sure all technical transition details land in `Svc.Log`.

**Step 4: Run the full verification suite**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal`
Expected: PASS with all tests green.

Run: `dotnet build StarLoom.csproj -v minimal`
Expected: PASS with `0 Error(s)`.

**Step 5: Commit**

```bash
git add StarLoom.cs Tasks/WorkflowTask.cs Tasks/Artisan/ArtisanTask.cs Tasks/Navigation/NavigationTask.cs Tasks/TurnIn/TurnInTask.cs Tasks/Purchase/PurchaseTask.cs StarLoom.Tests
git commit -m "feat: complete StarLoom rewrite core flow"
```
