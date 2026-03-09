using Dalamud.Configuration;
using StarLoom.Data;
using System;
using System.Collections.Generic;

namespace StarLoom;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    public int ArtisanListId { get; set; }
    public HousingReturnPoint? DefaultCraftReturnPoint { get; set; } = HousingReturnPoint.CreateInn();
    public CollectableShop? PreferredCollectableShop { get; set; }
    public bool BuyAfterEachTurnIn { get; set; }
    public PurchaseCompletionAction PostPurchaseAction { get; set; } = PurchaseCompletionAction.ReturnToConfiguredPoint;
    public bool ShowStatusOverlay { get; set; }
    public string UiLanguage { get; set; } = "zh";
    public int ReserveScripAmount { get; set; }
    public List<ItemToPurchase> ScripShopItems { get; set; } = [];
    public int FreeSlotThreshold { get; set; } = 10;

    public bool EnsurePostPurchaseDefaults()
    {
        var updated = false;

        if (DefaultCraftReturnPoint == null)
        {
            DefaultCraftReturnPoint = HousingReturnPoint.CreateInn();
            updated = true;
        }

        if (!Enum.IsDefined(PostPurchaseAction))
        {
            PostPurchaseAction = PurchaseCompletionAction.ReturnToConfiguredPoint;
            updated = true;
        }

        return updated;
    }
}
