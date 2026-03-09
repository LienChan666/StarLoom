using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using StarLoom.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StarLoom.Services;

public sealed unsafe class ScripShopCatalogBuilder
{
    private const uint ScripInclusionShopId = 3801094;

    public string GetCatalogVersion()
    {
        var inclusionShopSheet = Svc.Data.GetExcelSheet<InclusionShop>();
        var inclusionShopSeriesSheet = Svc.Data.GetSubrowExcelSheet<InclusionShopSeries>();
        var itemSheet = Svc.Data.GetExcelSheet<Item>();

        if (inclusionShopSheet is null)
            throw new InvalidOperationException("Failed to read the InclusionShop sheet.");
        if (inclusionShopSeriesSheet is null)
            throw new InvalidOperationException("Failed to read the InclusionShopSeries sheet.");
        if (itemSheet is null)
            throw new InvalidOperationException("Failed to read the Item sheet.");

        var shop = inclusionShopSheet.GetRow(ScripInclusionShopId);
        if (shop.RowId == 0)
            throw new InvalidOperationException($"Failed to find InclusionShop row {ScripInclusionShopId}.");

        var seriesLookup = inclusionShopSeriesSheet
            .SelectMany(group => group)
            .GroupBy(row => row.RowId)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.SubrowId).ToList());

        var signatureBuilder = new StringBuilder();
        signatureBuilder.Append("shop:").Append(ScripInclusionShopId).Append(';');

        for (var page = 0; page < shop.Category.Count; page++)
        {
            var category = shop.Category[page];
            var seriesId = category.IsValid ? category.Value.InclusionShopSeries.RowId : 0u;

            signatureBuilder
                .Append("page:")
                .Append(page)
                .Append(':')
                .Append(category.IsValid ? 1 : 0)
                .Append(':')
                .Append(seriesId)
                .Append(';');

            if (seriesId == 0 || !seriesLookup.TryGetValue(seriesId, out var seriesRows))
                continue;

            foreach (var series in seriesRows)
            {
                signatureBuilder
                    .Append("series:")
                    .Append(series.RowId)
                    .Append(':')
                    .Append(series.SubrowId)
                    .Append(';');

                var specialShop = series.SpecialShop.ValueNullable;
                if (specialShop == null)
                {
                    signatureBuilder.Append("special:missing;");
                    continue;
                }

                for (var rawIndex = 0; rawIndex < specialShop.Value.Item.Count; rawIndex++)
                {
                    var entry = specialShop.Value.Item[rawIndex];
                    var hasItem = TryGetEntryItemId(entry, out var itemId);
                    var itemName = string.Empty;
                    if (hasItem && itemId > 0)
                    {
                        var item = itemSheet.GetRow(itemId);
                        if (item.RowId != 0)
                            itemName = item.Name.ExtractText();
                    }

                    var itemCost = GetCost(entry);
                    var rawCurrencyId = GetCostItemId(entry);
                    var normalizedCurrency = rawCurrencyId == 0
                        ? new ScripCurrencyResolver.NormalizedCurrency(0, 0)
                        : ScripCurrencyResolver.NormalizeCurrency(rawCurrencyId, ResolveSpecialCurrencyItemId);
                    var currencyName = GetCurrencyName(itemSheet, normalizedCurrency.CurrencyItemId);
                    var discipline = ScripCurrencyResolver.ResolveDiscipline(normalizedCurrency.SpecialId, currencyName);

                    signatureBuilder
                        .Append("entry:")
                        .Append(rawIndex)
                        .Append(':')
                        .Append(hasItem ? itemId : 0)
                        .Append(':')
                        .Append(itemName)
                        .Append(':')
                        .Append(itemCost)
                        .Append(':')
                        .Append(rawCurrencyId)
                        .Append(':')
                        .Append(normalizedCurrency.CurrencyItemId)
                        .Append(':')
                        .Append(normalizedCurrency.SpecialId)
                        .Append(':')
                        .Append(currencyName)
                        .Append(':')
                        .Append((int)discipline)
                        .Append(':')
                        .Append(entry.PatchNumber)
                        .Append(':')
                        .Append(entry.Order)
                        .Append(';');
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureBuilder.ToString())));
    }

    public List<ScripShopItem> BuildCatalog()
    {
        var inclusionShopSheet = Svc.Data.GetExcelSheet<InclusionShop>();
        var inclusionShopSeriesSheet = Svc.Data.GetSubrowExcelSheet<InclusionShopSeries>();
        var itemSheet = Svc.Data.GetExcelSheet<Item>();

        if (inclusionShopSheet is null)
            throw new InvalidOperationException("Failed to read the InclusionShop sheet.");
        if (inclusionShopSeriesSheet is null)
            throw new InvalidOperationException("Failed to read the InclusionShopSeries sheet.");
        if (itemSheet is null)
            throw new InvalidOperationException("Failed to read the Item sheet.");

        var shop = inclusionShopSheet.GetRow(ScripInclusionShopId);
        if (shop.RowId == 0)
            throw new InvalidOperationException($"Failed to find InclusionShop row {ScripInclusionShopId}.");

        var seriesLookup = inclusionShopSeriesSheet
            .SelectMany(group => group)
            .GroupBy(row => row.RowId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var candidates = new List<CatalogCandidate>();
        var seen = new HashSet<uint>();

        for (var page = 0; page < shop.Category.Count; page++)
        {
            var category = shop.Category[page];
            if (!category.IsValid)
                continue;

            var seriesId = category.Value.InclusionShopSeries.RowId;
            if (seriesId == 0 || !seriesLookup.TryGetValue(seriesId, out var seriesRows))
                continue;

            foreach (var series in seriesRows)
            {
                var specialShop = series.SpecialShop.ValueNullable;
                if (specialShop == null)
                    continue;

                var seriesCandidates = new List<CatalogCandidate>();
                for (var rawIndex = 0; rawIndex < specialShop.Value.Item.Count; rawIndex++)
                {
                    var entry = specialShop.Value.Item[rawIndex];
                    if (!TryGetEntryItemId(entry, out var itemId))
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

                    var normalizedCurrency = ScripCurrencyResolver.NormalizeCurrency(rawCurrencyId, ResolveSpecialCurrencyItemId);
                    var currencyName = GetCurrencyName(itemSheet, normalizedCurrency.CurrencyItemId);
                    var discipline = ScripCurrencyResolver.ResolveDiscipline(normalizedCurrency.SpecialId, currencyName);

                    seriesCandidates.Add(new CatalogCandidate(
                        item.Name.ExtractText(),
                        itemId,
                        0,
                        itemCost,
                        page,
                        series.SubrowId + 1,
                        normalizedCurrency.SpecialId,
                        normalizedCurrency.CurrencyItemId,
                        currencyName,
                        discipline,
                        entry.PatchNumber,
                        entry.Order,
                        rawIndex));
                }

                foreach (var (candidate, displayIndex) in seriesCandidates
                             .OrderBy(candidate => candidate.SortOrder)
                             .ThenBy(candidate => candidate.RawIndex)
                             .Select((candidate, displayIndex) => (candidate, displayIndex)))
                {
                    if (!seen.Add(candidate.ItemId))
                        continue;

                    candidates.Add(candidate with { DisplayIndex = displayIndex });
                }
            }
        }

        var tierLookup = BuildTierLookup(candidates);

        var results = candidates
            .Select(candidate => new ScripShopItem
            {
                Name = candidate.Name,
                ItemID = candidate.ItemId,
                Index = candidate.DisplayIndex,
                ItemCost = candidate.ItemCost,
                Page = candidate.Page,
                SubPage = candidate.SubPage,
                CurrencySpecialId = candidate.CurrencySpecialId,
                CurrencyItemId = candidate.CurrencyItemId,
                CurrencyName = candidate.CurrencyName,
                Discipline = candidate.Discipline,
                TierRank = tierLookup.GetValueOrDefault(candidate.IdentityKey, 0),
            })
            .ToList();

        return results
            .OrderBy(item => item.Page)
            .ThenBy(item => item.SubPage)
            .ThenBy(item => item.Index)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
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

    private static string GetCurrencyName(ExcelSheet<Item> itemSheet, uint currencyItemId)
    {
        if (currencyItemId == 0)
            return string.Empty;

        var item = itemSheet.GetRow(currencyItemId);
        return item.RowId == 0 ? string.Empty : item.Name.ExtractText();
    }

    private static Dictionary<CurrencyIdentityKey, int> BuildTierLookup(List<CatalogCandidate> candidates)
    {
        var result = new Dictionary<CurrencyIdentityKey, int>();

        foreach (var disciplineGroup in candidates
                     .Where(candidate => candidate.Discipline != ScripDiscipline.Unknown)
                     .GroupBy(candidate => candidate.Discipline))
        {
            var rankedCurrencies = ScripCurrencyResolver.AssignTierRanks(
                disciplineGroup.Key,
                disciplineGroup
                    .GroupBy(candidate => candidate.IdentityKey)
                    .Select(group => new ScripCurrencyResolver.ScripCurrencyPatchInfo(
                        group.Key.CurrencyItemId,
                        group.Key.CurrencySpecialId,
                        group.Select(x => x.CurrencyName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty,
                        group.Max(x => x.PatchNumber))));

            foreach (var rankedCurrency in rankedCurrencies)
            {
                var key = new CurrencyIdentityKey(
                    disciplineGroup.Key,
                    rankedCurrency.CurrencyItemId,
                    rankedCurrency.SpecialId);
                result[key] = rankedCurrency.TierRank;
            }
        }

        return result;
    }

    private readonly record struct CurrencyIdentityKey(
        ScripDiscipline Discipline,
        uint CurrencyItemId,
        byte CurrencySpecialId);

    private sealed record CatalogCandidate(
        string Name,
        uint ItemId,
        int DisplayIndex,
        uint ItemCost,
        int Page,
        int SubPage,
        byte CurrencySpecialId,
        uint CurrencyItemId,
        string CurrencyName,
        ScripDiscipline Discipline,
        ushort PatchNumber,
        byte SortOrder,
        int RawIndex)
    {
        public CurrencyIdentityKey IdentityKey => new(Discipline, CurrencyItemId, CurrencySpecialId);
    }
}
