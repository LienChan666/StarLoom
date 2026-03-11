using Dalamud.Configuration;

namespace StarLoom.Config;

public sealed class PluginConfig : IPluginConfiguration
{
    public int version { get; set; } = 1;
    public int artisanListId { get; set; }
    public ReturnPointConfig? defaultReturnPoint { get; set; }
    public CollectableShopConfig? preferredCollectableShop { get; set; }
    public PostPurchaseAction postPurchaseAction { get; set; }
    public bool showStatusOverlay { get; set; }
    public string uiLanguage { get; set; } = string.Empty;
    public int reserveScripAmount { get; set; }
    public int freeSlotThreshold { get; set; }
    public List<PurchaseItemConfig> scripShopItems { get; set; } = [];

    public int Version
    {
        get => version;
        set => version = value;
    }
}
