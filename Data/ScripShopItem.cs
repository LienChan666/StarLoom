using System.Text.Json.Serialization;

namespace Starloom.Data;

public class ScripShopItem
{
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ItemId")]
    public uint ItemID { get; set; }
    public int Index { get; set; }
    public uint ItemCost { get; set; }
    public int Page { get; set; }
    public int SubPage { get; set; }
    public byte CurrencySpecialId { get; set; }
    public uint CurrencyItemId { get; set; }
    public string CurrencyName { get; set; } = string.Empty;
    public ScripDiscipline Discipline { get; set; }
    public int TierRank { get; set; }

    [JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public uint ItemId => ItemID;
}
