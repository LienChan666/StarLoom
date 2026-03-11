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
        var purchaseCatalog = new PurchaseCatalog();
        var artisanTask = new ArtisanTask(new ArtisanIpc(), pluginConfig);
        var navigationTask = new NavigationTask(new VNavmeshIpc(), new LifestreamIpc());
        var turnInTask = new TurnInTask(inventoryGame, npcGame, collectableShopGame);
        var purchaseTask = new PurchaseTask(pluginConfig, inventoryGame, scripShopGame);

        workflowTask = new WorkflowTask(pluginConfig, artisanTask, navigationTask, turnInTask, purchaseTask);
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
        pluginUi.Dispose();
        ECommonsMain.Dispose();
    }
}
