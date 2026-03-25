using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation.NeoTaskManager;
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
    internal ConfigurationEditor ConfigEditor;
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
    internal ITextureProvider Textures;

    public Starloom(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        P = this;
        Textures = pluginInterface.Create<DalamudTextureService>()?.Textures
            ?? throw new System.InvalidOperationException("Failed to resolve Dalamud texture provider.");

        TM = new TaskManager();
        ConfigStore = new ConfigurationStore();
        ConfigStore.EnsureDefaults();
        ConfigEditor = new ConfigurationEditor(ConfigStore);
        Localization = new LocalizationService(ConfigStore);
        Inventory = new InventoryService();
        Navigation = new NavigationService();
        NpcInteraction = new NpcInteractionService();
        Artisan = new ArtisanIpc();
        ShopItems = new ScripShopItemManager(C, ConfigEditor);
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
