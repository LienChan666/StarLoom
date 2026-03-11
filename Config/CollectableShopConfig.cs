using System.Numerics;

namespace StarLoom.Config;

public sealed class CollectableShopConfig
{
    public Vector3 location { get; set; }
    public uint territoryId { get; set; }
    public uint npcId { get; set; }
    public uint scripShopNpcId { get; set; }
    public Vector3 scripShopLocation { get; set; }
    public uint aetheryteId { get; set; }
    public bool isLifestreamRequired { get; set; }
    public string lifestreamCommand { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
}
