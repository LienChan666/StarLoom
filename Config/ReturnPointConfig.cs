namespace StarLoom.Config;

public sealed class ReturnPointConfig
{
    public string kind { get; set; } = string.Empty;
    public uint territoryId { get; set; }
    public uint aetheryteId { get; set; }
    public byte subIndex { get; set; }
    public bool isApartment { get; set; }
    public string displayName { get; set; } = string.Empty;
    public bool isInn
    {
        get => string.Equals(kind, "inn", StringComparison.Ordinal);
        set => kind = value ? "inn" : "housing";
    }

    public static ReturnPointConfig CreateInn()
    {
        return new ReturnPointConfig
        {
            isInn = true,
            displayName = "Inn",
        };
    }
}
