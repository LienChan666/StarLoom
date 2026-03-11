using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Globalization;

namespace StarLoom.Tasks.Purchase;

public sealed unsafe class PurchaseCatalog
{
    private const uint ScripInclusionShopId = 3801094;

    private readonly List<PurchaseCatalogItem> items = [];

    public bool isLoading { get; private set; }
    public IReadOnlyList<PurchaseCatalogItem> itemsView => items;

    public PurchaseCatalog()
    {
        Reload();
    }

    public void Reload()
    {
        isLoading = true;
        items.Clear();

        try
        {
            items.AddRange(BuildCatalog());
        }
        catch (Exception ex)
        {
            items.Clear();
            Svc.Log.Error($"Failed to build purchase catalog.{Environment.NewLine}{ex}");
        }
        finally
        {
            isLoading = false;
        }
    }

    public static PurchaseCatalogPage Resolve(PurchaseTarget purchaseTarget)
    {
        return new PurchaseCatalogPage(
            purchaseTarget.page,
            purchaseTarget.subPage);
    }

    private static List<PurchaseCatalogItem> BuildCatalog()
    {
        var inclusionShopSheet = Svc.Data.GetExcelSheet<InclusionShop>();
        var inclusionShopSeriesSheet = Svc.Data.GetSubrowExcelSheet<InclusionShopSeries>();
        var itemSheet = Svc.Data.GetExcelSheet<Item>();

        if (inclusionShopSheet is null || inclusionShopSeriesSheet is null || itemSheet is null)
            return [];

        var shop = inclusionShopSheet.GetRow(ScripInclusionShopId);
        if (shop.RowId == 0)
            return [];

        var seriesLookup = inclusionShopSeriesSheet
            .SelectMany(group => group)
            .GroupBy(row => row.RowId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var results = new List<PurchaseCatalogItem>();
        var seen = new HashSet<uint>();

        for (var pageIndex = 0; pageIndex < shop.Category.Count; pageIndex++)
        {
            var category = shop.Category[pageIndex];
            if (!category.IsValid)
                continue;

            var seriesId = category.Value.InclusionShopSeries.RowId;
            if (seriesId == 0 || !seriesLookup.TryGetValue(seriesId, out var seriesRows))
                continue;

            foreach (var series in seriesRows.OrderBy(row => row.SubrowId))
            {
                var specialShop = series.SpecialShop.ValueNullable;
                if (specialShop == null)
                    continue;

                var displayIndex = 0;
                for (var rawIndex = 0; rawIndex < specialShop.Value.Item.Count; rawIndex++)
                {
                    var entry = specialShop.Value.Item[rawIndex];
                    if (!TryGetEntryItemId(entry, out var itemId) || !seen.Add(itemId))
                        continue;

                    var item = itemSheet.GetRow(itemId);
                    if (item.RowId == 0)
                        continue;

                    var itemCost = GetCost(entry);
                    if (itemCost == 0)
                        continue;

                    var rawCurrencyId = GetCostItemId(entry);
                    if (rawCurrencyId == 0)
                        continue;

                    var normalizedCurrency = NormalizeCurrency(rawCurrencyId, ResolveSpecialCurrencyItemId);
                    var currencyName = GetCurrencyName(itemSheet, normalizedCurrency.currencyItemId);

                    results.Add(new PurchaseCatalogItem(
                        itemId,
                        item.Name.ExtractText(),
                        itemCost,
                        (pageIndex + 1).ToString(CultureInfo.InvariantCulture),
                        (series.SubrowId + 1).ToString(CultureInfo.InvariantCulture),
                        normalizedCurrency.specialId,
                        normalizedCurrency.currencyItemId,
                        currencyName,
                        ResolveDiscipline(normalizedCurrency.specialId, currencyName),
                        displayIndex));

                    displayIndex++;
                }
            }
        }

        return results
            .OrderBy(item => int.TryParse(item.page, out var page) ? page : int.MaxValue)
            .ThenBy(item => int.TryParse(item.subPage, out var subPage) ? subPage : int.MaxValue)
            .ThenBy(item => item.index)
            .ThenBy(item => item.itemName, StringComparer.Ordinal)
            .ToList();
    }

    private static bool TryGetEntryItemId(SpecialShop.ItemStruct entry, out uint itemId)
    {
        itemId = 0;
        foreach (var receive in entry.ReceiveItems)
        {
            uint receiveItemId;
            try
            {
                receiveItemId = receive.Item.RowId;
            }
            catch
            {
                continue;
            }

            if (receiveItemId == 0)
                continue;

            itemId = receiveItemId;
            return true;
        }

        return false;
    }

    private static uint GetCostItemId(SpecialShop.ItemStruct entry)
    {
        foreach (var cost in entry.ItemCosts)
        {
            uint costItemId;
            try
            {
                costItemId = cost.ItemCost.RowId;
            }
            catch
            {
                continue;
            }

            if (cost.CurrencyCost > 0 && costItemId > 0)
                return costItemId;
        }

        return 0;
    }

    private static uint GetCost(SpecialShop.ItemStruct entry)
    {
        foreach (var cost in entry.ItemCosts)
        {
            if (cost.CurrencyCost > 0)
                return cost.CurrencyCost;

            if (cost.CollectabilityCost > 0)
                return cost.CollectabilityCost;
        }

        return 0;
    }

    private static (byte specialId, uint currencyItemId) NormalizeCurrency(uint rawCurrencyId, Func<byte, uint> tryResolveSpecialItemId)
    {
        if (rawCurrencyId > byte.MaxValue)
            return (0, rawCurrencyId);

        var specialId = (byte)rawCurrencyId;
        var resolvedItemId = tryResolveSpecialItemId(specialId);
        if (resolvedItemId == 0)
            resolvedItemId = TryResolveKnownSpecialItemId(specialId);

        return (specialId, resolvedItemId);
    }

    private static PurchaseDiscipline ResolveDiscipline(byte specialId, string currencyName)
    {
        return specialId switch
        {
            1 or 2 or 6 => PurchaseDiscipline.Crafting,
            3 or 4 or 7 => PurchaseDiscipline.Gathering,
            _ when currencyName.Contains("Crafter", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("Crafters", StringComparison.OrdinalIgnoreCase)
                => PurchaseDiscipline.Crafting,
            _ when currencyName.Contains("Gatherer", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("Gatherers", StringComparison.OrdinalIgnoreCase)
                => PurchaseDiscipline.Gathering,
            _ => PurchaseDiscipline.Unknown,
        };
    }

    private static uint ResolveSpecialCurrencyItemId(byte specialId)
    {
        var currencyManager = CurrencyManager.Instance();
        if (currencyManager == null)
            return 0;

        try
        {
            return currencyManager->GetItemIdBySpecialId(specialId);
        }
        catch
        {
            return 0;
        }
    }

    private static uint TryResolveKnownSpecialItemId(byte specialId)
    {
        return specialId switch
        {
            1 => 25199,
            2 => 33913,
            3 => 25200,
            4 => 33914,
            6 => 41784,
            7 => 41785,
            _ => 0,
        };
    }

    private static string GetCurrencyName(ExcelSheet<Item> itemSheet, uint currencyItemId)
    {
        if (currencyItemId == 0)
            return string.Empty;

        var item = itemSheet.GetRow(currencyItemId);
        return item.RowId == 0 ? string.Empty : item.Name.ExtractText();
    }
}

public enum PurchaseDiscipline
{
    Unknown = 0,
    Crafting = 1,
    Gathering = 2,
}

public readonly record struct PurchaseCatalogItem(
    uint itemId,
    string itemName,
    uint itemCost,
    string page,
    string subPage,
    byte currencySpecialId,
    uint currencyItemId,
    string currencyName,
    PurchaseDiscipline discipline,
    int index);

public readonly record struct PurchaseCatalogPage(string page, string subPage);
