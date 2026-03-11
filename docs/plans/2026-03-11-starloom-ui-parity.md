# StarLoom UI Parity Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Restore the rewrite worktree UI so that it matches the original StarLoom window structure, interaction flow, localization, and overlay styling while keeping the rewrite's `Ui/Pages` entry structure.

**Architecture:** Keep `Ui/MainWindow.cs`, `Ui/Pages/HomePage.cs`, and `Ui/Pages/SettingsPage.cs` as the page-level entry points, then rebuild the original component tree under `Ui/Components/Home`, `Ui/Components/Settings`, and `Ui/Components/Shared`. UI continues to consume `WorkflowTask`, `ConfigStore`, and minimal read-only data providers without reintroducing the old global UI architecture.

**Tech Stack:** C# 14 / .NET 10, Dalamud, xUnit

---

### Task 1: Restore Shared Localization And Layout Primitives

**Files:**
- Modify: `Resources/Localization/zh.json`
- Modify: `Resources/Localization/en.json`
- Create: `Ui/Components/Shared/LayoutMetrics.cs`
- Create: `Ui/Components/Shared/GamePanelStyle.cs`
- Create: `Ui/Components/Shared/ScripShopUiHelpers.cs`
- Create: `StarLoom.Tests/Ui/LayoutMetricsTests.cs`

**Step 1: Write the failing layout test**

```csharp
using StarLoom.Ui.Components.Shared;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class LayoutMetricsTests
{
    [Fact]
    public void CreateHome_Should_Match_Original_Widths_And_Heights()
    {
        var metrics = LayoutMetrics.CreateHome(1200f, 800f, 12f);

        Assert.Equal(340f, metrics.LeftWidth);
        Assert.Equal(848f, metrics.RightWidth);
        Assert.Equal(346.72f, metrics.TopHeight, 2);
        Assert.Equal(441.28f, metrics.BottomHeight, 2);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~LayoutMetricsTests`
Expected: FAIL because `LayoutMetrics` does not exist yet in the rewrite UI.

**Step 3: Write the minimal shared UI implementation**

- Restore `LayoutMetrics` with the original home/settings geometry.
- Restore `GamePanelStyle` with the original color, separator, status dot, and action button helpers.
- Restore `ScripShopUiHelpers` for currency labels.
- Expand `zh.json` and `en.json` to include the original UI key set required by home, settings, and overlay screens.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~LayoutMetricsTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Resources/Localization/zh.json Resources/Localization/en.json Ui/Components/Shared/LayoutMetrics.cs Ui/Components/Shared/GamePanelStyle.cs Ui/Components/Shared/ScripShopUiHelpers.cs StarLoom.Tests/Ui/LayoutMetricsTests.cs
git commit -m "feat: restore ui shared styling primitives"
```

### Task 2: Rebuild The Home Control Pane

**Files:**
- Create: `Ui/Components/Home/HomeControlPane.cs`
- Modify: `Ui/Pages/HomePage.cs`
- Modify: `StarLoom.Tests/Ui/HomePageStateTests.cs`

**Step 1: Write the failing control-state test**

```csharp
using StarLoom.Ui.Pages;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class HomePageStateTests
{
    [Fact]
    public void FromWorkflow_Should_Disable_Purchase_When_No_Configured_Items_Exist()
    {
        var state = HomePageState.FromWorkflow(isBusy: false, hasConfiguredPurchases: false);

        Assert.False(state.canStartPurchaseOnly);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~HomePageStateTests`
Expected: FAIL because the existing home page state or home page layout does not yet expose the original control-pane behavior.

**Step 3: Write the minimal home control implementation**

- Create `HomeControlPane` with original status rows, artisan list input, primary actions, and quick actions.
- Keep `HomePage` as the page entry and let it compose `HomeControlPane`.
- Use localization keys for all labels and button text.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~HomePageStateTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ui/Components/Home/HomeControlPane.cs Ui/Pages/HomePage.cs StarLoom.Tests/Ui/HomePageStateTests.cs
git commit -m "feat: restore home control pane"
```

### Task 3: Rebuild Search And Selected Item Panes

**Files:**
- Create: `Ui/Components/Home/SearchPane.cs`
- Create: `Ui/Components/Home/SelectedItemsPane.cs`
- Modify: `Ui/Pages/HomePage.cs`
- Create: `StarLoom.Tests/Ui/SelectedItemsOrderingTests.cs`

**Step 1: Write the failing list-ordering test**

```csharp
using StarLoom.Ui.Pages;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class SelectedItemsOrderingTests
{
    [Fact]
    public void MoveUp_Should_Swap_With_Previous_Item()
    {
        var items = new[] { 1, 2, 3 };

        var reordered = HomePageReorder.MoveUp(items, 1);

        Assert.Equal([2, 1, 3], reordered);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~SelectedItemsOrderingTests`
Expected: FAIL because the reorder helper does not exist yet.

**Step 3: Write the minimal search and selection implementation**

- Restore `SearchPane` with item search input, loading hint, filtered table, and add button.
- Restore `SelectedItemsPane` with quantity editing, move up/down, and remove actions.
- Extract only the smallest pure helper needed for ordering so the test can stay logic-based.
- Keep persistence behavior identical to the original: save after relevant edits.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~SelectedItemsOrderingTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ui/Components/Home/SearchPane.cs Ui/Components/Home/SelectedItemsPane.cs Ui/Pages/HomePage.cs StarLoom.Tests/Ui/SelectedItemsOrderingTests.cs
git commit -m "feat: restore home search and selection panes"
```

### Task 4: Restore The Settings Sections

**Files:**
- Create: `Ui/Components/Settings/SettingsTab.cs`
- Create: `Ui/Components/Settings/ShopSettingsCard.cs`
- Create: `Ui/Components/Settings/CraftPointSettingsCard.cs`
- Create: `Ui/Components/Settings/PurchaseSettingsCard.cs`
- Create: `Ui/Components/Settings/DisplaySettingsCard.cs`
- Modify: `Ui/Pages/SettingsPage.cs`
- Create: `StarLoom.Tests/Ui/SettingsInputClampTests.cs`

**Step 1: Write the failing settings clamp test**

```csharp
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class SettingsInputClampTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 5)]
    public void ClampNonNegative_Should_Return_Expected_Value(int input, int expected)
    {
        Assert.Equal(expected, SettingsValueRules.ClampNonNegative(input));
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~SettingsInputClampTests`
Expected: FAIL because the settings helper does not exist yet.

**Step 3: Write the minimal settings implementation**

- Rebuild the four settings cards and the settings tab structure.
- Keep `SettingsPage` as the page entry and compose `SettingsTab`.
- Extract only tiny pure helpers where needed for clamp logic or label selection.
- Preserve original save timing and localization behavior.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~SettingsInputClampTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ui/Components/Settings/SettingsTab.cs Ui/Components/Settings/ShopSettingsCard.cs Ui/Components/Settings/CraftPointSettingsCard.cs Ui/Components/Settings/PurchaseSettingsCard.cs Ui/Components/Settings/DisplaySettingsCard.cs Ui/Pages/SettingsPage.cs StarLoom.Tests/Ui/SettingsInputClampTests.cs
git commit -m "feat: restore settings page parity"
```

### Task 5: Restore Main Window And Status Overlay Parity

**Files:**
- Modify: `Ui/MainWindow.cs`
- Modify: `Ui/PluginUi.cs`
- Modify: `Ui/StatusOverlay.cs`
- Create: `StarLoom.Tests/Ui/OverlayStateTextTests.cs`

**Step 1: Write the failing overlay text test**

```csharp
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class OverlayStateTextTests
{
    [Fact]
    public void Overlay_Title_Key_Should_Exist()
    {
        const string key = "overlay.total_state";

        Assert.Contains(key, UiLocalizationKeys.Required);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~OverlayStateTextTests`
Expected: FAIL because the required UI localization key helper does not exist yet.

**Step 3: Write the minimal main window and overlay implementation**

- Restore the original main window default size and localized tab titles.
- Restore the overlay styling, state dot, gradient separator, and stop button behavior.
- Keep `PluginUi` wiring unchanged except for consuming the restored pages and overlay.
- Add a tiny required-key helper so localization coverage can be asserted without testing rendering directly.

**Step 4: Run test to verify it passes**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~OverlayStateTextTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ui/MainWindow.cs Ui/PluginUi.cs Ui/StatusOverlay.cs StarLoom.Tests/Ui/OverlayStateTextTests.cs
git commit -m "feat: restore main window and overlay parity"
```

### Task 6: Run Full UI Verification

**Files:**
- Modify: `Ui/MainWindow.cs`
- Modify: `Ui/Pages/HomePage.cs`
- Modify: `Ui/Pages/SettingsPage.cs`
- Modify: `Ui/StatusOverlay.cs`
- Modify: `Resources/Localization/zh.json`
- Modify: `Resources/Localization/en.json`
- Modify: `StarLoom.Tests/Ui/LayoutMetricsTests.cs`
- Modify: `StarLoom.Tests/Ui/HomePageStateTests.cs`
- Modify: `StarLoom.Tests/Ui/SelectedItemsOrderingTests.cs`
- Modify: `StarLoom.Tests/Ui/SettingsInputClampTests.cs`
- Modify: `StarLoom.Tests/Ui/OverlayStateTextTests.cs`

**Step 1: Add the final parity checks**

Add or extend tests to cover:

- original home geometry for narrow and wide windows
- home action enable/disable rules
- selected-item reorder and quantity clamp helpers
- settings numeric clamp behavior
- presence of required localization keys for all restored sections

**Step 2: Run tests to verify they fail**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal --filter FullyQualifiedName~StarLoom.Tests.Ui`
Expected: FAIL until the remaining parity gaps are closed.

**Step 3: Fill the remaining parity gaps**

- Close any mismatch in layout, text keys, helper logic, or save behavior.
- Keep changes scoped to UI parity only.
- Do not introduce fallback rendering paths.

**Step 4: Run full verification**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal`
Expected: PASS with all UI and existing tests green.

Run: `dotnet build StarLoom.csproj -v minimal`
Expected: PASS with `0 Error(s)`.

**Step 5: Commit**

```bash
git add Ui Resources/Localization StarLoom.Tests/Ui
git commit -m "feat: complete ui parity for rewrite worktree"
```
