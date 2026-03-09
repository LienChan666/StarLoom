# Starloom 架构设计规范

## 1. 概述

Starloom 是一个 FFXIV Dalamud 插件，用于自动化制作工作流（收集品上交、军票购买、传送返回等）。
架构遵循 **高内聚低耦合** 原则，同时尊重 Dalamud 社区惯例。

### 技术栈

- C# / .NET 10
- Dalamud Plugin Framework
- ECommons 3.1.0.10（TaskManager、DalamudReflector、DuoLog）
- ImGui（通过 Dalamud.Bindings.ImGui）
- FFXIVClientStructs（游戏内存访问）
- Lumina（游戏数据表读取）

---

## 2. 项目结构

```
Starloom/
├── Global.cs                       # 全局 using 指令
├── Starloom.cs                     # 入口点 (P=this 组合根)
├── Configuration.cs                # 持久化配置模型
│
├── Automation/                     # 自动化控制层
│   ├── AutomationController.cs     # 工作流启停控制
│   ├── ArtisanSession.cs           # Artisan 托管会话状态机
│   ├── WorkflowStartValidator.cs   # 启动前校验
│   ├── ArtisanPauseGate.cs         # Artisan 暂停决策逻辑
│   ├── LocalPlayerActionGate.cs    # 玩家状态检查
│   ├── StateMachine.cs             # 通用状态机
│   └── StateTimeoutTracker.cs      # 状态超时追踪
│
├── Tasks/                          # TaskManager 任务步骤
│   ├── Workflows.cs                # 工作流编排（组合 Task 序列）
│   ├── TaskArtisanPause.cs         # 暂停/恢复 Artisan
│   ├── TaskCollectableTurnIn.cs    # 收集品上交
│   ├── TaskScripPurchase.cs        # 军票购买
│   ├── TaskReturnToCraftPoint.cs   # 返回制作点
│   └── TaskCloseGame.cs            # 关闭游戏
│
├── Data/                           # 纯数据模型（无逻辑）
│   ├── CollectableShop.cs
│   ├── ScripShopItem.cs
│   ├── ItemToPurchase.cs
│   ├── PendingPurchaseItem.cs
│   ├── HousingReturnPoint.cs
│   ├── NpcLocations.cs
│   ├── PurchaseCompletionAction.cs
│   ├── ScripDiscipline.cs
│   └── ScripShopCatalogCacheDocument.cs
│
├── GameInterop/                    # 游戏交互层
│   ├── Addons/                     # 游戏 UI Addon 操作
│   │   ├── CollectableShopAddon.cs
│   │   ├── ScripShopAddon.cs
│   │   └── TreeListHelper.cs
│   └── IPC/                        # 插件间通信
│       ├── IArtisanIpc.cs          # Artisan IPC 接口
│       ├── ArtisanIpc.cs
│       ├── VNavmeshIpc.cs
│       ├── LifestreamIpc.cs
│       ├── IpcCallRunner.cs        # 通用 IPC 调用封装
│       └── ExternalPluginDetector.cs
│
├── Services/                       # 业务服务
│   ├── ConfigurationStore.cs       # 配置加载/保存
│   ├── LocalizationService.cs      # UI 本地化
│   ├── InventoryService.cs         # 背包查询
│   ├── NavigationService.cs        # 传送 + 寻路状态机
│   ├── NpcInteractionService.cs    # NPC 交互
│   ├── ScripShopItemManager.cs     # 军票商店目录缓存
│   ├── ScripPurchaseService.cs     # 军票购买状态管理
│   ├── PendingPurchaseResolver.cs  # 待购买物品计算
│   ├── ScripShopCatalogBuilder.cs  # 游戏数据表解析
│   ├── ScripCurrencyResolver.cs    # 货币类型判定
│   ├── ItemJobResolver.cs          # 物品→职业映射
│   ├── NativeTeleporter.cs         # 原生传送 API
│   └── HousingReturnPointService.cs # 住宅返回点
│
├── UI/                             # UI 层
│   ├── PluginUi.cs                 # WindowSystem 管理
│   ├── MainWindow.cs               # 主窗口
│   ├── StatusOverlay.cs            # 状态悬浮窗
│   └── Components/
│       ├── Home/                   # 主页组件
│       ├── Settings/               # 设置组件
│       └── Shared/                 # 共享 UI 工具
│
└── Resources/
    └── Localization/               # 多语言 JSON
```

---

## 3. 核心设计模式

### 3.1 P=this 全局访问 (Dalamud 社区惯例)

```csharp
// Global.cs
global using static Starloom.Starloom;

// Starloom.cs
public static Starloom P = null!;
public static Configuration C => P.ConfigStore.Configuration;
```

- `P` — 插件实例，所有服务通过 `P.xxx` 访问
- `C` — 配置快捷访问

**任何地方** 都可以直接使用 `P.Navigation`、`P.Inventory`、`C.PreferredCollectableShop` 等，无需依赖注入。

### 3.2 组合根 (Composition Root)

所有服务在 `Starloom.cs` 构造函数中按依赖顺序实例化。不使用 ServiceRegistry 或 DI 容器。

### 3.3 ECommons LegacyTaskManager

用于编排多步骤自动化任务。**返回值约定与直觉相反，务必牢记：**

| 返回值 | 含义 |
|--------|------|
| `true` | 当前步骤完成，继续下一步 |
| `false` | 还没完成，下帧重试（附带超时检查） |
| `null` | **中止信号**，清空所有剩余任务 |

```csharp
// 典型 Task 结构
internal static class TaskXxx
{
    internal static void Enqueue()
    {
        ResetState();
        P.TM.Enqueue(Step1, "Xxx.Step1");
        P.TM.Enqueue(Step2, "Xxx.Step2");
    }

    private static bool? Step1()
    {
        // 完成 → return true;
        // 等待 → return false;
        // 中止 → return null;
    }
}
```

### 3.4 Workflows 编排

`Tasks/Workflows.cs` 负责组合多个 Task 形成完整工作流：

```
EnqueueConfiguredWorkflow:
  → TaskArtisanPause (如需)
  → TaskCollectableTurnIn
  → TaskScripPurchase (如配置)
  → TaskReturnToCraftPoint 或 TaskCloseGame
```

### 3.5 最小抽象原则

- **接口** 仅用于 IPC（`IArtisanIpc`），因为外部插件可能不可用
- 其余全部使用 **具体类**，不创建 IService 接口
- 避免不必要的抽象层（已删除 IPluginUiFacade、ServiceRegistry、JobOrchestrator）

---

## 4. 命名规范

### 4.1 命名空间

| 层 | 命名空间 |
|----|---------|
| 入口 / 配置 | `Starloom` |
| 自动化控制 | `Starloom.Automation` |
| 任务步骤 | `Starloom.Tasks` |
| 数据模型 | `Starloom.Data` |
| 游戏 Addon | `Starloom.GameInterop.Addons` |
| 插件间通信 | `Starloom.GameInterop.IPC` |
| 业务服务 | `Starloom.Services` |
| UI | `Starloom.UI` / `Starloom.UI.Components.*` |

### 4.2 成员命名

| 类型 | 风格 | 示例 |
|------|------|------|
| 私有字段 | camelCase（无下划线前缀） | `lastActionAt`, `turnInQueue` |
| 内部/公共字段 | PascalCase | `P.Navigation`, `P.TM` |
| 属性 | PascalCase | `State`, `IsBusy` |
| 方法 | PascalCase | `NavigateTo()`, `Enqueue()` |
| 常量 / static readonly | PascalCase | `ScripInclusionShopId` |
| 局部变量 | camelCase | `shop`, `distance` |

### 4.3 Task 命名

- 类名: `Task` + 动作 → `TaskCollectableTurnIn`, `TaskScripPurchase`
- 步骤标签: `"前缀.步骤"` → `"TurnIn.Navigate"`, `"ScripPurchase.WaitShop"`

---

## 5. 日志规范

### 5.1 内部日志 — `Svc.Log`

用于开发者排查问题的内部消息，用户不可见（仅 `/xllog` 可查看）。

```csharp
Svc.Log.Debug("Found 3 collectable types to turn in.");
Svc.Log.Info("Started configured workflow.");
Svc.Log.Warning("Navigation timed out.");
Svc.Log.Error("Teleport failed.");
```

### 5.2 用户可见日志 — `DuoLog`

同时写入聊天栏和 `/xllog`，用于需要玩家关注的消息。

```csharp
DuoLog.Error("Collectable shop is not configured.");
DuoLog.Error("Not enough scrips to purchase one item.");
```

### 5.3 禁止

- **不加** `[Starloom]` 前缀（Dalamud 已自动添加插件名）
- **不在** 日志中重复方法名或上下文（从日志内容应能推断来源）

---

## 6. 错误处理规范

### 6.1 优雅降级优先

Dalamud 插件中避免抛异常。异常在 `Framework.Update` 或 `UiBuilder.Draw` 中如果未捕获会中断功能。

```csharp
// 好：日志 + 返回空值
if (sheet is null)
{
    Svc.Log.Error("Failed to read game data sheet.");
    return [];
}

// 坏：抛异常
throw new InvalidOperationException("Failed to read sheet.");
```

### 6.2 Task 步骤中的失败

- 用户可操作的错误 → `DuoLog.Error(message)` + `return null;`（中止）
- 可恢复的等待 → `return false;`（下帧重试）

### 6.3 IPC 调用

`IpcCallRunner` 已内置重试 + 降级逻辑，`IsAvailable()` 检查在调用前执行。
仅在 `requireAvailable = true` 时才允许抛 `InvalidOperationException`。

---

## 7. 层间依赖规则

```
UI → P.xxx / C（直接访问）
Tasks → P.xxx / C / GameInterop / Services
Automation → Tasks / Services
Services → Data / GameInterop
GameInterop → ECommons / FFXIVClientStructs
Data → 无依赖
```

### 7.1 禁止

- 下层不得引用上层（Data 不可引用 Services，Services 不可引用 Automation）
- Task 之间不可直接调用（通过 Workflows 编排）
- UI 组件不可持有服务引用作为字段（全部通过 `P.xxx` 和 `C` 实时读取）

---

## 8. 扩展指南

### 8.1 添加新的自动化任务

1. 在 `Tasks/` 下创建 `TaskXxx.cs`
2. 使用 `internal static class`，包含 `Enqueue()` 和 `ResetState()`
3. 每个步骤为 `private static bool?` 方法
4. 在 `Workflows.cs` 中将其编排到工作流

### 8.2 添加新的游戏 Addon 交互

1. 在 `GameInterop/Addons/` 下创建对应类
2. 使用 `unsafe` 上下文访问 AtkUnitBase
3. 通过 `TryGetAddonByName` + `IsAddonReady` 进行安全访问

### 8.3 添加新的 IPC

1. 在 `GameInterop/IPC/` 下创建接口 + 实现
2. 使用 `IpcCallRunner` 封装调用
3. 在 `Starloom.cs` 中注册服务

### 8.4 添加新的服务

1. 在 `Services/` 下创建具体类（不需要接口）
2. 在 `Starloom.cs` 构造函数中实例化并赋给 `internal` 字段
3. 如果需要每帧更新，在 `OnUpdate()` 中调用 `xxx.Update()`


## 9. 关键约束备忘

1. **LegacyTaskManager**: `false` = 重试, `null` = 中止（与直觉相反）
2. **不使用下划线前缀**：所有私有字段用 `camelCase`
3. **不使用接口**：除 IPC 外，全部使用具体类
4. **不在日志加前缀**：Dalamud 已添加 `[Starloom]`
5. **不抛异常**：优雅降级 + 日志
6. **UI 无状态**：不缓存服务引用，每帧通过 `P.xxx` / `C` 读取
