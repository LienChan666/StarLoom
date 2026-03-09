using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Linq;

namespace StarLoom.Services;

public static class ItemJobResolver
{
    public static int GetJobIdForItem(string itemName, IDataManager data)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return -1;

        itemName = itemName.Replace(" \uE03D", string.Empty).ToLowerInvariant();

        var item = data.GetExcelSheet<Item>()?
            .FirstOrDefault(i => i.Name.ToString().ToLowerInvariant() == itemName);
        if (item == null)
            return -1;

        var itemId = item.Value.RowId;

        var recipeSheet = data.GetExcelSheet<Recipe>();
        if (recipeSheet != null)
        {
            var recipe = recipeSheet.FirstOrDefault(r => r.ItemResult.RowId == itemId);
            if (((Lumina.Excel.IExcelRow<Recipe>)recipe).RowId != 0)
                return (int)recipe.CraftType.RowId;
        }

        return -1;
    }
}
