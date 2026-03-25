using Dalamud.Bindings.ImGui;
using Starloom.Data;

namespace Starloom.UI.Components.Shared;

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
            return P.Localization.Get("currency.crafting");

        return item.Discipline == ScripDiscipline.Gathering
            ? P.Localization.Get("currency.gathering")
            : P.Localization.Get("currency.unknown");
    }
}
