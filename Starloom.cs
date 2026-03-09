using Dalamud.Game.Command;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using Starloom.Automation;
using Starloom.GameInterop.IPC;
using Starloom.Services;
using Starloom.UI;

namespace Starloom;

public sealed class Starloom : IDalamudPlugin
{
    public static Starloom P = null!;
    public static Configuration C => P.ConfigStore.Configuration;

    internal TaskManager TM;
    internal ConfigurationStore ConfigStore;
    internal LocalizationService Localization;
    internal InventoryService Inventory;
    internal NavigationService Navigation;
    internal NpcInteractionService NpcInteraction;
    internal IArtisanIpc Artisan;
    internal ScripShopItemManager ShopItems;
    internal PendingPurchaseResolver PurchaseResolver;
    internal CollectableTurnInService CollectableTurnIn;
    internal ScripPurchaseService ScripPurchase;
    internal WorkflowOrchestrator Automation;
    internal PluginUi Ui;

    public Starloom(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        P = this;

        TM = new TaskManager();
        ConfigStore = new ConfigurationStore();
        ConfigStore.EnsureDefaults();
        Localization = new LocalizationService(ConfigStore);
        Inventory = new InventoryService();
        Navigation = new NavigationService();
        NpcInteraction = new NpcInteractionService();
        Artisan = new ArtisanIpc();
        ShopItems = new ScripShopItemManager(C, ConfigStore);
        PurchaseResolver = new PendingPurchaseResolver(C, Inventory);
        CollectableTurnIn = new CollectableTurnInService();
        ScripPurchase = new ScripPurchaseService();
        Automation = new WorkflowOrchestrator();
        Ui = new PluginUi();

        Svc.Commands.AddHandler("/starloom", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Starloom window",
        });
        Svc.Framework.Update += OnUpdate;
    }

    private void OnCommand(string command, string args) => Ui.ToggleMainWindow();

    private void OnUpdate(IFramework framework)
    {
        if (!Svc.ClientState.IsLoggedIn) return;
        Automation.Update();
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.Commands.RemoveHandler("/starloom");
        Ui.Dispose();
        Automation.Dispose();
        ECommonsMain.Dispose();
        P = null!;
    }
}
