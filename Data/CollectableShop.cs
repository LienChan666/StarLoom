using System.Numerics;

namespace Starloom.Data;

public class CollectableShop
{
    public string Name { get; set; } = string.Empty;
    public Vector3 Location { get; set; }
    public uint AetheryteId { get; set; }
    public uint TerritoryId { get; set; }
    public uint NpcId { get; set; }
    public uint ScripShopNpcId { get; set; }
    public bool Disabled { get; set; }
    public bool IsLifestreamRequired { get; set; }
    public string LifestreamCommand { get; set; } = string.Empty;
    public Vector3 ScripShopLocation { get; set; }
}
