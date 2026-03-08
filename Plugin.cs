using Dalamud.Game.Command;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using StarLoom.Core;
using StarLoom.Data;
using StarLoom.IPC;
using StarLoom.Jobs;
using StarLoom.Services;
using StarLoom.UI;
using System.Collections.Generic;
using System.Linq;

namespace StarLoom;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Starloom";
    private const string CommandName = "/starloom";

    internal static Plugin P = null!;
    internal Configuration Config = null!;
    internal ArtisanIPC ArtisanIPC = null!;
    internal NavigationService Navigation = null!;
    internal NpcInteractionService NpcInteraction = null!;
    internal ScripShopItemManager ScripShopItemManager = null!;
    internal JobOrchestrator Orchestrator = null!;
    internal ManagedArtisanSession ManagedSession = null!;
    internal PluginUi Ui = null!;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        P = this;

        Config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new();
        if (Config.EnsurePostPurchaseDefaults())
            Config.Save();

        ArtisanIPC = new ArtisanIPC();
        Navigation = new NavigationService();
        NpcInteraction = new NpcInteractionService();
        ScripShopItemManager = new ScripShopItemManager(Config);

        var context = new JobContext
        {
            Artisan = ArtisanIPC,
            Navigation = Navigation,
            NpcInteraction = NpcInteraction,
            Config = Config,
        };

        Orchestrator = new JobOrchestrator(ArtisanIPC, context);
        ManagedSession = new ManagedArtisanSession(ArtisanIPC, Orchestrator, Config, BuildConfiguredWorkflowJobs);
        Ui = new PluginUi(this);

        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开 Starloom 主界面。",
        });

        Svc.Framework.Update += OnFrameworkUpdate;
    }

    internal bool HasConfiguredPurchases => Config.ScripShopItems is { Count: > 0 };
    internal bool HasConfiguredCollectableShop => Config.PreferredCollectableShop != null;
    internal bool IsAutomationBusy => ManagedSession.IsActive || Orchestrator.IsRunning;

    private void OnCommand(string command, string args)
        => Ui.ToggleMainWindow();

    internal void StartConfiguredWorkflow()
    {
        if (!EnsureCollectableShopConfigured("联动流程"))
            return;

        if (!ManagedSession.TryStart())
            Svc.Log.Warning($"[Starloom] {ManagedSession.ErrorMessage ?? "无法启动联动流程。"}");
    }

    internal void StartCollectableTurnIn()
        => StartJobs(new IAutomationJob[] { new CollectableTurnInJob() }, "收藏品提交");

    internal void StartPurchaseOnly()
    {
        if (!EnsurePurchaseWorkflowCanStart("工票购买"))
            return;

        StartJobs(BuildPurchaseWorkflowJobs(), "工票购买");
    }

    internal void StopAutomation()
    {
        if (ManagedSession.IsActive)
        {
            ManagedSession.Stop();
            return;
        }

        if (Orchestrator.IsRunning)
            Orchestrator.Abort();
    }

    internal string GetOrchestratorStateText()
    {
        if (ManagedSession.State != ManagedArtisanSessionState.Idle)
            return ManagedSession.GetStateText();

        return Orchestrator.State switch
        {
            OrchestratorState.Idle => "空闲",
            OrchestratorState.WaitingForArtisanPause => "等待 Artisan 暂停",
            OrchestratorState.RunningJobs => "执行任务中",
            OrchestratorState.Completed => "已完成",
            OrchestratorState.Failed => "已失败",
            _ => Orchestrator.State.ToString(),
        };
    }

    internal string GetCurrentJobDisplayName()
    {
        var currentJob = Orchestrator.CurrentJob;
        if (currentJob == null)
            return ManagedSession.State != ManagedArtisanSessionState.Idle ? "Artisan 清单联动" : "无";

        return currentJob.Id switch
        {
            "collectable-turn-in" => "收藏品提交",
            "scrip-purchase" => "工票购买",
            "return-to-craft-point" => "返回制作点",
            "close-game" => "关闭游戏",
            _ => currentJob.Id,
        };
    }

    internal string GetCurrentStatusText()
    {
        if (Orchestrator.CurrentJob != null && !string.IsNullOrWhiteSpace(Orchestrator.CurrentJob.StatusText))
            return Orchestrator.CurrentJob.StatusText;

        if (ManagedSession.State != ManagedArtisanSessionState.Idle && !string.IsNullOrWhiteSpace(ManagedSession.StatusText))
            return ManagedSession.StatusText;

        return Orchestrator.State switch
        {
            OrchestratorState.Idle => "空闲",
            OrchestratorState.WaitingForArtisanPause => "正在等待 Artisan 停止当前制作",
            OrchestratorState.RunningJobs => "任务运行中",
            OrchestratorState.Completed => "任务已完成",
            OrchestratorState.Failed => "任务失败，请查看 Dalamud 控制台日志。",
            _ => "空闲",
        };
    }

    private IReadOnlyList<IAutomationJob> BuildConfiguredWorkflowJobs()
    {
        var jobs = new List<IAutomationJob>
        {
            new CollectableTurnInJob(),
        };

        if (ShouldRunConfiguredPurchaseWorkflow())
        {
            jobs.Add(new ScripPurchaseJob());
            jobs.Add(BuildPostPurchaseActionJob());
        }

        return jobs;
    }

    private IReadOnlyList<IAutomationJob> BuildPurchaseWorkflowJobs()
    {
        var jobs = new List<IAutomationJob>
        {
            new ScripPurchaseJob(),
        };

        jobs.Add(BuildPostPurchaseActionJob());
        return jobs;
    }

    private IAutomationJob BuildPostPurchaseActionJob()
        => Config.PostPurchaseAction switch
        {
            PurchaseCompletionAction.CloseGame => new CloseGameJob(),
            _ => new ReturnToCraftPointJob(),
        };

    private bool ShouldRunConfiguredPurchaseWorkflow()
        => Config.BuyAfterEachTurnIn && GetPendingPurchaseItems().Count > 0;

    private List<ItemToPurchase> GetPendingPurchaseItems()
        => Config.ScripShopItems
            .Where(item => item.Item != null && item.Quantity > 0)
            .Where(item => InventoryService.GetInventoryItemCount(item.Item!.ItemId) < item.Quantity)
            .ToList();

    private bool EnsurePurchaseWorkflowCanStart(string actionName)
    {
        if (!HasConfiguredPurchases)
        {
            Svc.Log.Warning($"[Starloom] 启动 {actionName} 前，兑换物品列表为空。");
            return false;
        }

        var validPurchaseItems = Config.ScripShopItems
            .Where(item => item.Item != null && item.Quantity > 0)
            .ToList();

        if (validPurchaseItems.Count == 0)
        {
            Svc.Log.Warning($"[Starloom] 启动 {actionName} 前，兑换物品列表为空或没有有效目标数量。");
            return false;
        }

        if (GetPendingPurchaseItems().Count == 0)
        {
            Svc.Log.Warning($"[Starloom] 启动 {actionName} 前，兑换清单中的所有物品全部已达到目标数量。");
            return false;
        }

        return true;
    }

    private void StartJobs(IEnumerable<IAutomationJob> jobs, string actionName)
    {
        if (!EnsureCollectableShopConfigured(actionName))
            return;

        if (ManagedSession.IsActive)
        {
            Svc.Log.Warning($"[Starloom] 当前正在进行 Artisan 联动，忽略 {actionName} 启动请求。");
            return;
        }

        if (!Orchestrator.TryStart(jobs))
            Svc.Log.Warning($"[Starloom] 当前已有任务在运行，忽略 {actionName} 启动请求。");
    }

    private bool EnsureCollectableShopConfigured(string actionName)
    {
        if (HasConfiguredCollectableShop)
            return true;

        Svc.Log.Warning($"[Starloom] 启动 {actionName} 前需要先配置收藏品商店。");
        return false;
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        Navigation.Update();
        Orchestrator.Update();
        ManagedSession.Update();
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Commands.RemoveHandler(CommandName);
        Ui.Dispose();
        Orchestrator.Dispose();
        ECommonsMain.Dispose();
    }
}
