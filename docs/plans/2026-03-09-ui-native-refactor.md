# UI Native Refactor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将当前带有重装饰和冗余说明的插件界面改写为原生 ImGui 风格，同时保留现有功能与主要页签结构。

## Context

- 项目是 Dalamud 插件，主窗口入口在 `UI/MainWindow.cs`。
- 首页结构在 `UI/Components/Home/`，设置页结构在 `UI/Components/Settings/`。
- 现有 UI 大量依赖 `UI/Components/Shared/GamePanelStyle.cs` 的卡片、标题、提示和强调样式。
- 本次不做双轨兼容，不保留旧 UI 兜底，直接替换为原生控件布局。
- 目前没有可运行的测试项目，因此先补最小测试入口，只验证本次新引入的纯布局逻辑。

## Task 1: 为布局计算补最小测试

- Files:
  - `Starloom.Tests/Starloom.Tests.csproj`
  - `Starloom.Tests/UI/LayoutMetricsTests.cs`
  - `Global.cs`
- Test: `Starloom.Tests/UI/LayoutMetricsTests.cs`

**Step 1: 新建测试项目与失败测试**

创建 `Starloom.Tests/Starloom.Tests.csproj`，引用主项目；创建 `Starloom.Tests/UI/LayoutMetricsTests.cs`，为尚不存在的布局 helper 写断言，覆盖首页与设置页的宽度/高度钳制规则。

**Step 2: 运行测试，确认失败**

Run: `dotnet test .\Starloom.Tests\Starloom.Tests.csproj`
Expected: 由于 helper 尚不存在，编译失败。

**Step 3: 暴露 internal 给测试项目**

在 `Global.cs` 添加 `InternalsVisibleTo("Starloom.Tests")`，保证布局 helper 可被测试访问。

## Task 2: 提取纯布局逻辑并让测试通过

- Files:
  - `UI/Components/Shared/LayoutMetrics.cs`
  - `UI/Components/Home/HomeTab.cs`
  - `UI/Components/Settings/SettingsTab.cs`
- Test: `Starloom.Tests/UI/LayoutMetricsTests.cs`

**Step 1: 写最小布局 helper**

在 `UI/Components/Shared/LayoutMetrics.cs` 提供首页和设置页的纯计算方法，只负责尺寸钳制与区域分配，不包含任何 ImGui 绘制。

**Step 2: 运行测试，确认通过**

Run: `dotnet test .\Starloom.Tests\Starloom.Tests.csproj`
Expected: PASS。

**Step 3: 在页面中接入 helper**

让 `HomeTab` 和 `SettingsTab` 改用新的布局 helper，移除重复的局部尺寸计算。

## Task 3: 重写首页为原生 ImGui

- Files:
  - `UI/MainWindow.cs`
  - `UI/Components/Home/HomeTab.cs`
  - `UI/Components/Home/HomeControlPane.cs`
  - `UI/Components/Home/SearchPane.cs`
  - `UI/Components/Home/SelectedItemsPane.cs`
- Test: `dotnet build .\Starloom.csproj`

**Step 1: 简化主窗口容器**

保留窗口尺寸与 tab 结构，删除自定义窗口背景、边框、tab 下划线等额外装饰。

**Step 2: 删除首页头部总览区**

从 `HomeTab` 中移除 `HomeHeaderPanel`，仅保留左右分栏和搜索/队列内容区。

**Step 3: 改写控制区**

在 `HomeControlPane` 中只保留必要标题、状态、列表 ID、开始/停止、快捷动作；去掉副标题、提示、强调样式。

**Step 4: 改写搜索区与队列区**

将 `SearchPane` 与 `SelectedItemsPane` 改为原生 `Child` + `InputText` + `Table` + `Button`，保留数据行为，删去说明文案和额外视觉装饰。

## Task 4: 重写设置页为原生 ImGui

- Files:
  - `UI/Components/Settings/SettingsTab.cs`
  - `UI/Components/Settings/SettingsCard.cs`
  - `UI/Components/Settings/ShopSettingsCard.cs`
  - `UI/Components/Settings/CraftPointSettingsCard.cs`
  - `UI/Components/Settings/PurchaseSettingsCard.cs`
  - `UI/Components/Settings/DisplaySettingsCard.cs`
- Test: `dotnet build .\Starloom.csproj`

**Step 1: 简化设置页导航**

保留左侧分类选择，但去掉顶部说明、选中强调条和多余装饰。

**Step 2: 简化内容区结构**

移除 `SettingsCard` 中的标题+描述壳层，让各设置卡片直接渲染原生表单。

**Step 3: 收缩文字**

每个设置项只保留必要 label 与控件，不再输出底部“自动保存”等冗余提示。

## Task 5: 清理未再使用的 UI 文案与验证

- Files:
  - `Resources/Localization/en.json`
  - `Resources/Localization/zh.json`
  - 可能涉及若干 UI 文件
- Test:
  - `dotnet test .\Starloom.Tests\Starloom.Tests.csproj`
  - `dotnet build .\Starloom.csproj`

**Step 1: 删除不再使用的 header/description/hint 文案键**

只清理本次明确移除的 UI 文案，不扩散到无关模块。

**Step 2: 跑完整验证**

先跑测试，再跑主项目编译，确认没有新增错误。
