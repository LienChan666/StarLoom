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

namespace StarLoom;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Starloom";
    private const string CommandName = "/starloom";

    internal static Plugin P = null!;
    internal ServiceRegistry ServiceRegistry = null!;
    internal ConfigurationStore ConfigurationStore => ServiceRegistry.ConfigurationStore;
    internal Configuration Config => ServiceRegistry.Config;
    internal LocalizationService Localization => ServiceRegistry.Localization;
    internal IArtisanIpc ArtisanIPC => ServiceRegistry.Artisan;
    internal INavigationService Navigation => ServiceRegistry.Navigation;
    internal INpcInteractionService NpcInteraction => ServiceRegistry.NpcInteraction;
    internal ScripShopItemManager ScripShopItemManager => ServiceRegistry.ScripShopItemManager;
    internal JobOrchestrator Orchestrator = null!;
    internal ManagedArtisanSession ManagedSession = null!;
    internal AutomationController AutomationController = null!;
    internal AutomationStatusPresenter AutomationStatusPresenter = null!;
    internal PluginUi Ui = null!;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        P = this;

        ServiceRegistry = new ServiceRegistry(new ConfigurationStore());
        ConfigurationStore.EnsureDefaults();

        Orchestrator = new JobOrchestrator(ArtisanIPC, ServiceRegistry.CreateJobContext());
        ManagedSession = new ManagedArtisanSession(ArtisanIPC, Orchestrator, Config, ServiceRegistry.WorkflowBuilder.CreateConfiguredWorkflow, ServiceRegistry.WorkflowValidator);
        AutomationController = new AutomationController(Config, Navigation, Orchestrator, ManagedSession, ServiceRegistry.WorkflowBuilder, ServiceRegistry.WorkflowValidator);
        AutomationStatusPresenter = new AutomationStatusPresenter(Orchestrator, ManagedSession);
        Ui = new PluginUi(this);

        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Starloom window",
        });

        Svc.Framework.Update += OnFrameworkUpdate;
    }

    internal bool HasConfiguredPurchases => AutomationController.HasConfiguredPurchases;
    internal bool HasConfiguredCollectableShop => AutomationController.HasConfiguredCollectableShop;
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
