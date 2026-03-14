namespace Starloom.Data;

public class ItemToPurchase
{
    public ScripShopItem Item { get; set; } = new();
    public string Name => Item.Name;
    public int Quantity { get; set; }
}
