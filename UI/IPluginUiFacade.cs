using StarLoom.Data;
using System.Collections.Generic;

namespace StarLoom.UI;

public interface IPluginUiFacade
{
    Configuration Config { get; }
    bool HasConfiguredPurchases { get; }
    bool IsAutomationBusy { get; }
    bool IsCatalogLoading { get; }
    IReadOnlyList<ScripShopItem> ShopItems { get; }

    void SaveConfig();
    void ReloadLocalization();
    void StartConfiguredWorkflow();
    void StartCollectableTurnIn();
    void StartPurchaseOnly();
    void StopAutomation();
    void SetStatusOverlayVisible(bool isVisible);

    string GetText(string key);
    string GetText(string key, params object[] args);
    string GetOrchestratorStateText();
}
