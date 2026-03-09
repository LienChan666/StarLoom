using Dalamud.Game.Command;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using StarLoom.Core;
using StarLoom.Data;
using StarLoom.IPC;
using StarLoom.Services;
using StarLoom.Services.Interfaces;
using StarLoom.UI;
using StarLoom.Workflows;

namespace StarLoom;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Starloom";
    private const string CommandName = "/starloom";

    internal static Plugin P = null!;
    internal ConfigurationStore ConfigurationStore { get; }
    internal Configuration Config => ConfigurationStore.Configuration;
    internal LocalizationService Localization { get; }
    internal IInventoryService Inventory { get; }
    internal IArtisanIpc ArtisanIPC { get; }
    internal INavigationService Navigation { get; }
    internal INpcInteractionService NpcInteraction { get; }
    internal PendingPurchaseResolver PendingPurchaseResolver { get; }
    internal ScripShopItemManager ScripShopItemManager { get; }
    internal JobOrchestrator Orchestrator { get; }
    internal ManagedArtisanSession ManagedSession { get; }
    internal AutomationController AutomationController { get; }
    internal AutomationStatusPresenter AutomationStatusPresenter { get; }
    internal IPluginUiFacade UiFacade { get; }
    internal PluginUi Ui { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        P = this;

        var services = new ServiceRegistry(new ConfigurationStore());
        ConfigurationStore = services.ConfigurationStore;
        Localization = services.Localization;
        Inventory = services.Inventory;
        ArtisanIPC = services.Artisan;
        Navigation = services.Navigation;
        NpcInteraction = services.NpcInteraction;
        PendingPurchaseResolver = services.PendingPurchaseResolver;
        ScripShopItemManager = services.ScripShopItemManager;

        ConfigurationStore.EnsureDefaults();

        Orchestrator = new JobOrchestrator(ArtisanIPC, services.CreateJobContext());
        ManagedSession = new ManagedArtisanSession(ArtisanIPC, Orchestrator, Config, Inventory, services.WorkflowBuilder.CreateConfiguredWorkflow, services.WorkflowValidator);
        AutomationController = new AutomationController(Config, Navigation, Orchestrator, ManagedSession, services.WorkflowBuilder, services.WorkflowValidator);
        AutomationStatusPresenter = new AutomationStatusPresenter(Orchestrator, ManagedSession);
        UiFacade = new PluginUiFacade(Config, ConfigurationStore.Save, Localization, AutomationController, AutomationStatusPresenter, ScripShopItemManager);
        Ui = new PluginUi(UiFacade);

        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Starloom window",
        });

        Svc.Framework.Update += OnFrameworkUpdate;
    }

    internal bool HasConfiguredPurchases => AutomationController.HasConfiguredPurchases;
    internal bool IsAutomationBusy => AutomationController.IsAutomationBusy;

    internal void SaveConfig()
        => ConfigurationStore.Save();

    private void OnCommand(string command, string args)
        => Ui.ToggleMainWindow();

    internal void StartConfiguredWorkflow()
        => AutomationController.StartConfiguredWorkflow();

    internal void StartCollectableTurnIn()
        => AutomationController.StartCollectableTurnIn();

    internal void StartPurchaseOnly()
        => AutomationController.StartPurchaseOnly();

    internal void StopAutomation()
        => AutomationController.StopAutomation();

    internal string GetOrchestratorStateText()
        => Localization.Get(AutomationStatusPresenter.GetOrchestratorStateKey());

    internal string GetText(string key)
        => Localization.Get(key);

    internal string GetText(string key, params object[] args)
        => Localization.Format(key, args);

    internal void ReloadLocalization()
        => Localization.Reload();

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
        => AutomationController.Update();

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Commands.RemoveHandler(CommandName);
        Ui.Dispose();
        Orchestrator.Dispose();
        ECommonsMain.Dispose();
    }
}
