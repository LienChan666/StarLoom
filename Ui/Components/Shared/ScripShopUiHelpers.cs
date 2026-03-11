using Dalamud.Bindings.ImGui;

namespace StarLoom.Ui.Components.Shared;

internal enum ScripCurrencyKind
{
    Unknown,
    Crafting,
    Gathering,
}

internal static class ScripShopUiHelpers
{
    internal static ScripCurrencyKind ResolveCurrencyKind(byte specialId, string? currencyName)
    {
        return specialId switch
        {
            1 or 2 or 6 => ScripCurrencyKind.Crafting,
            3 or 4 or 7 => ScripCurrencyKind.Gathering,
            _ when !string.IsNullOrWhiteSpace(currencyName) && (
                currencyName.Contains("Crafter", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("Crafters", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("巧手", StringComparison.OrdinalIgnoreCase))
                => ScripCurrencyKind.Crafting,
            _ when !string.IsNullOrWhiteSpace(currencyName) && (
                currencyName.Contains("Gatherer", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("Gatherers", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("采集", StringComparison.OrdinalIgnoreCase))
                => ScripCurrencyKind.Gathering,
            _ => ScripCurrencyKind.Unknown,
        };
    }

    internal static void DrawCurrencyLabel(string? currencyName, ScripCurrencyKind currencyKind, Func<string, string> getText)
    {
        ImGui.TextUnformatted(GetCurrencyLabel(currencyName, currencyKind, getText));
        if (!string.IsNullOrWhiteSpace(currencyName) && ImGui.IsItemHovered())
            ImGui.SetTooltip(currencyName);
    }

    internal static string GetCurrencyLabel(string? currencyName, ScripCurrencyKind currencyKind, Func<string, string> getText)
    {
        if (!string.IsNullOrWhiteSpace(currencyName))
            return currencyName;

        return currencyKind switch
        {
            ScripCurrencyKind.Crafting => getText("currency.crafting"),
            ScripCurrencyKind.Gathering => getText("currency.gathering"),
            _ => getText("currency.unknown"),
        };
    }
}
