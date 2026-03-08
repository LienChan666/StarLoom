namespace Starloom.Data;

public sealed class HousingReturnPoint
{
    public uint AetheryteId { get; set; }
    public byte SubIndex { get; set; }
    public uint TerritoryId { get; set; }
    public bool IsInn { get; set; }
    public bool IsApartment { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public static HousingReturnPoint CreateInn()
        => new()
        {
            IsInn = true,
            DisplayName = "旅馆",
        };
}
