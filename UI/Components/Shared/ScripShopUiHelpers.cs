using Dalamud.Bindings.ImGui;
using StarLoom.Data;
using StarLoom.UI;

namespace StarLoom.UI.Components.Shared;

internal static class ScripShopUiHelpers
{
    internal static void DrawCurrencyLabel(IPluginUiFacade ui, ScripShopItem item)
    {
        ImGui.TextUnformatted(GetCurrencyLabel(ui, item));

        if (!string.IsNullOrWhiteSpace(item.CurrencyName) && ImGui.IsItemHovered())
            ImGui.SetTooltip(item.CurrencyName);
    }

    internal static string GetCurrencyLabel(IPluginUiFacade ui, ScripShopItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.CurrencyName))
            return item.CurrencyName;

        if (item.Discipline == ScripDiscipline.Crafting)
            return ui.GetText("currency.crafting");

        return item.Discipline == ScripDiscipline.Gathering
            ? ui.GetText("currency.gathering")
            : ui.GetText("currency.unknown");
    }
}
