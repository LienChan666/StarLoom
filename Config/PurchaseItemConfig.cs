namespace StarLoom.Config;

public sealed class PurchaseItemConfig
{
    public uint itemId { get; set; }
    public string itemName { get; set; } = string.Empty;
    public int index { get; set; }
    public int targetCount { get; set; }
    public int scripCost { get; set; }
    public string page { get; set; } = string.Empty;
    public string subPage { get; set; } = string.Empty;
    public byte currencySpecialId { get; set; }
    public uint currencyItemId { get; set; }
    public string currencyName { get; set; } = string.Empty;
}
