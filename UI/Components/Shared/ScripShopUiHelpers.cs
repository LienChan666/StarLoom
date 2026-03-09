using Dalamud.Bindings.ImGui;
using StarLoom.Data;

namespace StarLoom.UI.Components.Shared;

internal static class ScripShopUiHelpers
{
    internal static void DrawCurrencyLabel(ScripShopItem item)
    {
        ImGui.TextUnformatted(GetCurrencyLabel(item));

        if (!string.IsNullOrWhiteSpace(item.CurrencyName) && ImGui.IsItemHovered())
            ImGui.SetTooltip(item.CurrencyName);
    }

    internal static string GetCurrencyLabel(ScripShopItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.CurrencyName))
            return item.CurrencyName;

        if (item.Discipline == ScripDiscipline.Crafting)
            return "巧手工票";

        return item.Discipline == ScripDiscipline.Gathering
            ? "采集工票"
            : "未知工票";
    }
}
