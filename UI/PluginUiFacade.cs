using StarLoom.Data;
using StarLoom.Services;
using System;
using System.Collections.Generic;

namespace StarLoom.UI;

public sealed class PluginUiFacade : IPluginUiFacade
{
    private readonly Action _saveConfig;
    private readonly LocalizationService _localization;
    private readonly AutomationController _automationController;
    private readonly AutomationStatusPresenter _statusPresenter;

    public PluginUiFacade(
        Configuration config,
        Action saveConfig,
        LocalizationService localization,
        AutomationController automationController,
        AutomationStatusPresenter statusPresenter,
        ScripShopItemManager scripShopItemManager)
    {
        Config = config;
        _saveConfig = saveConfig;
        _localization = localization;
        _automationController = automationController;
        _statusPresenter = statusPresenter;
    }

    public Configuration Config { get; }
    public bool HasConfiguredPurchases => _automationController.HasConfiguredPurchases;
    public bool IsAutomationBusy => _automationController.IsAutomationBusy;
    public bool IsCatalogLoading => ScripShopItemManager.IsLoading;
    public IReadOnlyList<ScripShopItem> ShopItems => ScripShopItemManager.ShopItems;

    public void SaveConfig()
        => _saveConfig();

    public void ReloadLocalization()
        => _localization.Reload();

    public void StartConfiguredWorkflow()
        => _automationController.StartConfiguredWorkflow();

    public void StartCollectableTurnIn()
        => _automationController.StartCollectableTurnIn();

    public void StartPurchaseOnly()
        => _automationController.StartPurchaseOnly();

    public void StopAutomation()
        => _automationController.StopAutomation();

    public void SetStatusOverlayVisible(bool isVisible)
        => Config.ShowStatusOverlay = isVisible;

    public string GetText(string key)
        => _localization.Get(key);

    public string GetText(string key, params object[] args)
        => _localization.Format(key, args);

    public string GetOrchestratorStateText()
        => _localization.Get(_statusPresenter.GetOrchestratorStateKey());
}
