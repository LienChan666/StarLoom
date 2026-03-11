namespace StarLoom.Config;

public sealed class ReturnPointConfig
{
    public string kind { get; set; } = string.Empty;
    public uint territoryId { get; set; }
    public uint aetheryteId { get; set; }
    public byte subIndex { get; set; }
    public bool isApartment { get; set; }
    public string displayName { get; set; } = string.Empty;
    public bool isInn => string.Equals(kind, "inn", StringComparison.Ordinal);

    public static ReturnPointConfig CreateInn()
    {
        return new ReturnPointConfig
        {
            kind = "inn",
            displayName = "Inn",
        };
    }
}
