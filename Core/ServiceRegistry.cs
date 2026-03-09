using StarLoom.IPC;
using StarLoom.Services;
using StarLoom.Services.Interfaces;
using StarLoom.Workflows;

namespace StarLoom.Core;

public sealed class ServiceRegistry
{
    private readonly JobContext _jobContext;

    public ServiceRegistry(ConfigurationStore configurationStore)
    {
        ConfigurationStore = configurationStore;
        Localization = new LocalizationService(configurationStore);
        Inventory = new InventoryService();
        Artisan = new ArtisanIPC();
        Navigation = new NavigationService();
        NpcInteraction = new NpcInteractionService();
        PendingPurchaseResolver = new PendingPurchaseResolver(configurationStore.Configuration, Inventory);
        WorkflowBuilder = new WorkflowBuilder(configurationStore.Configuration, Inventory);
        WorkflowValidator = new WorkflowStartValidator(configurationStore.Configuration, Inventory);
        ScripShopItemManager = new Data.ScripShopItemManager(configurationStore.Configuration, configurationStore);
        _jobContext = new JobContext
        {
            Artisan = Artisan,
            Navigation = Navigation,
            NpcInteraction = NpcInteraction,
            Inventory = Inventory,
            Config = Config,
        };
    }

    public ConfigurationStore ConfigurationStore { get; }
    public Configuration Config => ConfigurationStore.Configuration;
    public LocalizationService Localization { get; }
    public IInventoryService Inventory { get; }
    public IArtisanIpc Artisan { get; }
    public INavigationService Navigation { get; }
    public INpcInteractionService NpcInteraction { get; }
    public PendingPurchaseResolver PendingPurchaseResolver { get; }
    public WorkflowBuilder WorkflowBuilder { get; }
    public WorkflowStartValidator WorkflowValidator { get; }
    public Data.ScripShopItemManager ScripShopItemManager { get; }

    public JobContext CreateJobContext()
        => _jobContext;
}
