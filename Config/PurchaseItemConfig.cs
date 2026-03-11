namespace StarLoom.Config;

public sealed class PurchaseItemConfig
{
    public uint itemId { get; set; }
    public string itemName { get; set; } = string.Empty;
    public int targetCount { get; set; }
    public string page { get; set; } = string.Empty;
    public string subPage { get; set; } = string.Empty;
}
