# UI Cleanup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 删除原生 UI 重构后已无引用的旧界面文件和冗余本地化键。

**Architecture:** 先用静态引用检查确认删除范围，再移除未使用文件，最后基于白名单键集合从本地化文件中删除无引用项。

**Tech Stack:** C#, PowerShell, JSON localization files.

---

### Task 1: 删除无引用 UI 文件

**Files:**
- Delete: `UI/Components/Home/HomeHeaderPanel.cs`
- Delete: `UI/Components/Settings/SettingsCard.cs`

**Step 1: 确认文件无引用**

检查 `HomeHeaderPanel` 和 `SettingsCard` 在源代码中的引用是否为零。

**Step 2: 删除文件**

直接删除无引用文件，不保留兜底。

### Task 2: 清理无用本地化键

**Files:**
- Modify: `Resources/Localization/en.json`
- Modify: `Resources/Localization/zh.json`

**Step 1: 删除首页废弃键**

删除 `home.header.*`、`home.flow.*` 和已不再使用的 `description`/`hint` 键。

**Step 2: 删除设置页废弃键**

删除 `settings.sidebar.title`、`settings.sidebar.description`、`settings.footer.hint` 以及 `settings.card.*` 标题和描述键。

### Task 3: 验证

**Files:**
- Test: `Starloom.csproj`

**Step 1: 运行编译**

Run: `dotnet build .\Starloom.csproj`
Expected: PASS with 0 errors.
