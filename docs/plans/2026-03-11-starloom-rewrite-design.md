# StarLoom 重写设计

## 目标

在一个空工作树中从零重写 `StarLoom`，保留旧仓作为参考，不要求兼容旧配置、旧入口或旧目录结构。新项目目标是减少抽象层、减少跨目录跳转、把流程控制集中到少数文件中，同时完整复刻现有功能逻辑。

## 非目标

- 不在本阶段删除旧仓代码。
- 不在本阶段默认改动 UI 布局和视觉风格。
- 不引入 DI 容器、事件总线、领域层等额外抽象。
- 不把 IPC 调用塞进 `TaskManager`。

## 硬规则

- 项目名统一使用 `StarLoom`。
- 单插件项目，测试保留在 `StarLoom.Tests`。
- 插件主入口文件直接命名为 `StarLoom.cs`。
- 不创建 `App/` 目录。
- 私有字段、局部变量、参数优先使用 `camelCase`，不使用下划线前缀。
- 关键技术日志输出到 Dalamud 控制台。
- 需要用户知晓并排查的问题使用 `DuoLog` 输出。
- 主流程只保留一个显式状态机，并且只放在 `Tasks/WorkflowTask.cs`。
- 需要调用 IPC 的模块不使用 `TaskManager`。
- `TaskManager` 只用于本项目内部的本地顺序步骤编排。
- 不恢复 `P`、`C` 这类全局静态入口。
- 不使用 `Manager`、`Helper`、`Resolver`、`Dispatcher`、`Controller`、`Actions` 这类泛命名作为主要业务文件名。

## 目录结构

```text
StarLoom/
├── StarLoom.cs
├── GlobalUsings.cs
├── Config/
├── Game/
├── Ipc/
├── Tasks/
│   ├── WorkflowTask.cs
│   ├── Artisan/
│   ├── Navigation/
│   ├── TurnIn/
│   └── Purchase/
├── Ui/
├── Resources/
└── StarLoom.Tests/
```

### 顶层目录职责

- `StarLoom.cs`
  负责插件生命周期、对象组装、命令注册、框架更新入口。
- `Config/`
  只放配置模型、配置存储、默认值处理。
- `Game/`
  只放游戏内直接交互封装，例如库存读取、NPC 交互、窗口操作、位置读取。
- `Ipc/`
  只放外部插件通信，例如 `Artisan`、`VNavmesh`、`Lifestream`。
- `Tasks/`
  放所有业务任务和总控。
- `Ui/`
  放窗口、页面、状态浮窗，UI 只和总控交互。
- `Resources/`
  放本地化和静态资源。
- `StarLoom.Tests/`
  放行为测试和纯逻辑测试。

## Tasks 设计

### 1. `Tasks/WorkflowTask.cs`

这是唯一总控文件，也是整个项目唯一显式状态机所在的位置。它负责：

- 启动主流程。
- 启动“仅上交”和“仅购买”入口。
- 观察 `ArtisanTask`、`NavigationTask`、`TurnInTask`、`PurchaseTask` 的状态。
- 决定何时暂停或恢复 Artisan。
- 决定何时导航、何时上交、何时购买、何时结束。
- 统一处理停止和失败。

状态枚举直接内嵌在这个文件内，不额外拆 `Workflow` 文件夹。

### 2. `Tasks/Artisan/ArtisanTask.cs`

`ArtisanTask` 不是制作流程实现，而是 Artisan IPC 控制层。它负责：

- 检查 Artisan IPC 是否可用。
- 启动指定清单。
- 暂停、恢复、停止 Artisan。
- 返回当前 Artisan 状态快照。

`ArtisanTask` 不使用 `TaskManager`，因为它的本质是外部状态控制和轮询，不适合排成本地步骤队列。

### 3. `Tasks/Navigation/NavigationTask.cs`

`NavigationTask` 是导航业务任务，不是底层服务。它负责：

- 接收导航请求。
- 结合 `VNavmesh`、`Lifestream` 等 IPC 发起移动。
- 轮询导航完成、失败或中止。
- 向主流程返回明确的状态。

`NavigationTask` 也不使用 `TaskManager`，因为它依赖外部 IPC 状态推进。

### 4. `Tasks/TurnIn/TurnInTask.cs`

`TurnInTask` 是本地顺序编排型任务。它负责：

- 收集当前可上交物品。
- 在到达目标后操作收藏品窗口。
- 选择职业、条目、提交并收尾。

`TurnInTask` 可以使用 `TaskManager`，因为这些步骤是本项目内部可顺序推进的本地动作。

### 5. `Tasks/Purchase/PurchaseTask.cs`

`PurchaseTask` 同样是本地顺序编排型任务。它负责：

- 计算待购买队列。
- 在到达目标后操作军票商店。
- 选择页面、商品、数量并完成购买。

`PurchaseTask` 可以使用 `TaskManager`，但它本身不直接调用 IPC。

## 子目录内文件约定

每个功能目录只保留必要文件：

- `XxxTask.cs`
  模块的唯一主入口。
- `XxxPlan.cs`
  放纯计算和纯决策。
- `XxxModels.cs`
  放模块内数据结构。

只有在确实必要时才额外拆文件。默认目标是让每个功能目录控制在 2 到 4 个文件内。

## 依赖方向

```text
Ui -> WorkflowTask

WorkflowTask -> ArtisanTask
WorkflowTask -> NavigationTask
WorkflowTask -> TurnInTask
WorkflowTask -> PurchaseTask

ArtisanTask -> Ipc + Config
NavigationTask -> Ipc + Game + Config
TurnInTask -> Game + Config + TaskManager
PurchaseTask -> Game + Config + TaskManager
```

补充约束：

- `Ui` 不直接调用 `Game/`。
- `Ui` 不直接调用 `Ipc/`。
- `Ipc/` 不做业务判断。
- `Game/` 不做主流程编排。
- `Config/` 不做业务判断。

## 运行时控制模型

### 主流程

主流程由 `WorkflowTask` 驱动，典型闭环如下：

1. 启动 Artisan 清单。
2. 进入监控阶段。
3. 当满足接管条件时，暂停或停止 Artisan。
4. 启动导航前往上交点。
5. 上交完成后，若仍需购买，则启动导航前往购买点。
6. 购买完成后，启动导航返回。
7. 返回完成后恢复 Artisan。
8. 继续下一轮，或结束流程。

### 小任务

- `ArtisanTask` 和 `NavigationTask` 通过“命令 + 轮询状态”推进。
- `TurnInTask` 和 `PurchaseTask` 通过 `TaskManager` 编排本地步骤。

这样可以让复杂状态只集中在总控中，小任务尽量保持线性。

## 配置设计

`Config/` 只保留实际有用且直白的配置字段，例如：

- `artisanListId`
- `preferredCollectableShop`
- `defaultReturnPoint`
- `postPurchaseAction`
- `reserveScripAmount`
- `scripShopItems`
- `showStatusOverlay`
- `uiLanguage`
- `freeSlotThreshold`

因为新项目不要求兼容旧配置，所以配置结构可以直接按新架构重命名和收敛。

## Game 设计

`Game/` 中的类名直接表达对象，不再用泛名词，例如：

- `InventoryGame.cs`
- `NpcGame.cs`
- `CollectableShopGame.cs`
- `ScripShopGame.cs`
- `PlayerStateGame.cs`
- `LocationGame.cs`

这些类只负责读取和操作游戏对象，不负责业务流程选择。

## Ipc 设计

`Ipc/` 中的类只负责外部插件通信，例如：

- `ArtisanIpc.cs`
- `VNavmeshIpc.cs`
- `LifestreamIpc.cs`

它们负责：

- 可用性检查。
- 命令调用。
- 状态查询。
- 基础异常保护和技术日志。

它们不负责“该不该调”和“下一步做什么”。

## 日志规则

### 使用 `Svc.Log`

- 生命周期日志。
- IPC 调用细节。
- 状态推进。
- 技术错误上下文。
- 调试信息。

### 使用 `DuoLog`

- 缺失必要配置。
- IPC 插件不可用且影响继续执行。
- 用户需要主动处理的失败。
- 建议用户检查的异常场景。

普通状态推进不使用 `DuoLog`，避免刷屏。

## UI 设计

UI 以复刻现有交互为主，不默认调整布局。约束如下：

- UI 只通过 `WorkflowTask` 读取总状态和发起入口。
- UI 不直接调用 `Game/` 或 `Ipc/`。
- `MainWindow` 和 `StatusOverlay` 保持轻量。
- 页面拆分优先按 `Home`、`Settings` 组织。

可选优化项保留到后续单独决策，不和本次重写耦合。

## 测试策略

新项目测试重点从“源码文本断言”改成“行为验证”：

- `Plan` 的纯逻辑测试。
- `WorkflowTask` 的状态切换测试。
- `ArtisanTask` 和 `NavigationTask` 的状态轮询测试。
- `TurnInTask` 和 `PurchaseTask` 的本地流程测试。

不再大量依赖“某个源文件里必须出现某行字符串”的测试方式。

## 重写顺序

1. 建立空工作树中的项目骨架。
2. 完成 `Config/`、`Ipc/`、`Game/`。
3. 实现 `NavigationTask` 和 `ArtisanTask`。
4. 实现 `TurnInTask` 和 `PurchaseTask`。
5. 实现 `WorkflowTask`。
6. 接入 UI。
7. 补测试和全链路验证。

## 结果标准

当以下条件同时满足时，重写版本可以进入下一阶段验证：

- 新项目能够独立构建和运行。
- 主流程能完整复刻制作、接管、上交、购买、返回、恢复的闭环。
- 所有用户可见错误都通过 `DuoLog` 给出明确提示。
- 关键技术过程都能在 Dalamud 控制台看到足够日志。
- 目录命名和模块边界符合本设计，不重新长回旧结构。
