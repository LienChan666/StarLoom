using Lumina.Excel.Sheets;

namespace StarLoom.Tasks.TurnIn;

public class TurnInJobResolver
{
    public virtual uint ResolveJobId(uint itemId, string itemName)
    {
        if (itemId == 0)
            return 0;

        var recipeSheet = Svc.Data.GetExcelSheet<Recipe>();
        if (recipeSheet == null)
            return 0;

        var recipe = recipeSheet.FirstOrDefault(row => row.ItemResult.RowId == itemId);
        return recipe.RowId == 0 ? 0 : recipe.CraftType.RowId;
    }
}
