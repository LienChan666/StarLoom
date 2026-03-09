using System;
using System.Collections.Generic;
using System.Linq;
using Starloom.Data;

namespace Starloom.Services;

public static class ScripCurrencyResolver
{
    public readonly record struct NormalizedCurrency(byte SpecialId, uint CurrencyItemId);

    public readonly record struct ScripCurrencyPatchInfo(
        uint CurrencyItemId,
        byte SpecialId,
        string CurrencyName,
        ushort MaxPatch);

    public readonly record struct RankedCurrency(uint CurrencyItemId, byte SpecialId, int TierRank);

    public static NormalizedCurrency NormalizeCurrency(uint rawCurrencyId, Func<byte, uint> tryResolveSpecialItemId)
    {
        if (rawCurrencyId > byte.MaxValue)
            return new(0, rawCurrencyId);

        var specialId = (byte)rawCurrencyId;
        var resolvedItemId = tryResolveSpecialItemId(specialId);
        if (resolvedItemId == 0)
            resolvedItemId = TryResolveKnownSpecialItemId(specialId);

        return new(specialId, resolvedItemId);
    }

    public static ScripDiscipline ResolveDiscipline(byte specialId, string currencyName)
        => specialId switch
        {
            1 or 2 or 6 => ScripDiscipline.Crafting,
            3 or 4 or 7 => ScripDiscipline.Gathering,
            _ when currencyName.Contains("Crafter", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("Crafters", StringComparison.OrdinalIgnoreCase)
                => ScripDiscipline.Crafting,
            _ when currencyName.Contains("Gatherer", StringComparison.OrdinalIgnoreCase)
                || currencyName.Contains("Gatherers", StringComparison.OrdinalIgnoreCase)
                => ScripDiscipline.Gathering,
            _ => ScripDiscipline.Unknown,
        };

    public static IReadOnlyList<RankedCurrency> AssignTierRanks(
        ScripDiscipline discipline,
        IEnumerable<ScripCurrencyPatchInfo> currencies)
    {
        _ = discipline;

        return currencies
            .OrderByDescending(x => x.MaxPatch)
            .ThenBy(x => x.CurrencyItemId)
            .ThenBy(x => x.SpecialId)
            .Select((x, index) => new RankedCurrency(x.CurrencyItemId, x.SpecialId, index))
            .ToList();
    }

    private static uint TryResolveKnownSpecialItemId(byte specialId)
        => specialId switch
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
