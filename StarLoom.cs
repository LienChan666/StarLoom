using Dalamud.Game.Command;
using Dalamud.Plugin;
using ECommons;
using StarLoom.Config;
using StarLoom.Game;
using StarLoom.Ipc;
using StarLoom.Tasks;
using StarLoom.Tasks.Artisan;
using StarLoom.Tasks.Navigation;
using StarLoom.Tasks.Purchase;
using StarLoom.Tasks.TurnIn;
using StarLoom.Ui;

namespace StarLoom;

public sealed class StarLoom : IDalamudPlugin
{
    private readonly ConfigStore configStore;
    private readonly WorkflowTask workflowTask;
    private readonly PluginUi pluginUi;

    public StarLoom(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);

        configStore = new ConfigStore(pluginInterface);

        var pluginConfig = configStore.pluginConfig;
        var inventoryGame = new InventoryGame();
        var npcGame = new NpcGame();
        var collectableShopGame = new CollectableShopGame();
        var scripShopGame = new ScripShopGame();
        var playerStateGame = new PlayerStateGame();
        var locationGame = new LocationGame();
        var pendingPurchaseResolver = new PendingPurchaseResolver(pluginConfig, inventoryGame);
        var purchaseCatalog = new PurchaseCatalog(new PurchaseCatalogSync(pluginConfig, configStore.Save));
        var artisanTask = new ArtisanTask(new ArtisanIpc(), pluginConfig);
        var navigationTask = new NavigationTask(new VNavmeshIpc(), new LifestreamIpc());
        var turnInTask = new TurnInTask(inventoryGame, collectableShopGame, new TurnInJobResolver());
        var purchaseTask = new PurchaseTask(pluginConfig, inventoryGame, npcGame, scripShopGame, pendingPurchaseResolver);

        workflowTask = new WorkflowTask(
            pluginConfig,
            artisanTask,
            navigationTask,
            turnInTask,
            purchaseTask,
            inventoryGame,
            playerStateGame,
            locationGame);
        pluginUi = new PluginUi(workflowTask, configStore, purchaseCatalog);

        Svc.Commands.AddHandler("/starloom", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the StarLoom window",
        });

        Svc.Framework.Update += OnUpdate;
    }

    private void OnCommand(string command, string arguments)
    {
        pluginUi.ToggleMainWindow();
    }

    private void OnUpdate(IFramework framework)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        workflowTask.Update();
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.Commands.RemoveHandler("/starloom");
        StopWorkflowBeforeDispose(workflowTask);
        pluginUi.Dispose();
        ECommonsMain.Dispose();
    }

    private static void StopWorkflowBeforeDispose(WorkflowTask workflowTask)
    {
        if (workflowTask.isBusy)
            workflowTask.Stop();
    }
}
