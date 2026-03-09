using StarLoom.IPC;
using StarLoom.Services;
using StarLoom.Services.Interfaces;
using StarLoom.Workflows;

namespace StarLoom.Core;

public sealed class ServiceRegistry
{
    public ServiceRegistry(ConfigurationStore configurationStore)
    {
        ConfigurationStore = configurationStore;
        Localization = new LocalizationService(configurationStore);
        Inventory = new InventoryServiceAdapter();
        Artisan = new ArtisanIPC();
        Navigation = new NavigationService();
        NpcInteraction = new NpcInteractionService();
        WorkflowBuilder = new WorkflowBuilder(configurationStore.Configuration, Inventory);
        WorkflowValidator = new WorkflowStartValidator(configurationStore.Configuration, Inventory);
        ScripShopItemManager = new Data.ScripShopItemManager(configurationStore.Configuration, configurationStore);
    }

    public ConfigurationStore ConfigurationStore { get; }
    public Configuration Config => ConfigurationStore.Configuration;
    public LocalizationService Localization { get; }
    public IInventoryService Inventory { get; }
    public IArtisanIpc Artisan { get; }
    public NavigationService Navigation { get; }
    public INpcInteractionService NpcInteraction { get; }
    public WorkflowBuilder WorkflowBuilder { get; }
    public WorkflowStartValidator WorkflowValidator { get; }
    public Data.ScripShopItemManager ScripShopItemManager { get; }

    public JobContext CreateJobContext()
        => new()
        {
            Artisan = Artisan,
            Navigation = Navigation,
            NpcInteraction = NpcInteraction,
            Inventory = Inventory,
            Config = Config,
        };
}
