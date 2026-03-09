using ECommons.DalamudServices;
using StarLoom.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StarLoom.Data;

public sealed class ScripShopItemManager
{
    private const string CacheFileName = "ScripShopItems.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Configuration _config;
    private readonly ConfigurationStore _configurationStore;
    private readonly ScripShopCatalogBuilder _catalogBuilder = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private string CachePath => Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, CacheFileName);

    public static List<ScripShopItem> ShopItems = [];
    public static bool IsLoading { get; private set; }

    public ScripShopItemManager(Configuration config, ConfigurationStore configurationStore)
    {
        _config = config;
        _configurationStore = configurationStore;
        RequestLoad();
    }

    public void RequestLoad()
        => _ = Task.Run(LoadScripItemsAsync);

    public async Task LoadScripItemsAsync()
    {
        await _syncLock.WaitAsync();
        IsLoading = true;
        try
        {
            var items = await EnsureCacheAndLoadAsync();
            ShopItems = items;
            SyncConfiguredItems(items);
            Svc.Log.Info($"[Starloom] Catalog loaded: {items.Count} items.");
        }
        catch (Exception ex)
        {
            ShopItems = [];
            Svc.Log.Error($"[Starloom] Catalog load failed: {ex}");
        }
        finally
        {
            IsLoading = false;
            _syncLock.Release();
        }
    }

    private async Task<List<ScripShopItem>> RebuildCacheAsync(string reason)
    {
        Svc.Log.Info($"[Starloom] Rebuilding catalog cache: {reason}.");
        var items = _catalogBuilder.BuildCatalog();
        await WriteCacheAsync(items);
        Svc.Log.Info($"[Starloom] Catalog rebuilt: {items.Count} items.");
        return items;
    }

    private async Task<List<ScripShopItem>> EnsureCacheAndLoadAsync()
    {
        if (!File.Exists(CachePath))
            return await RebuildCacheAsync("cache file missing");

        ScripShopCatalogCacheDocument cacheDocument;
        try
        {
            cacheDocument = await ReadCacheAsync();
        }
        catch
        {
            return await RebuildCacheAsync("cache file unreadable");
        }

        if (IsCacheValid(cacheDocument))
            return cacheDocument.Items;

        return await RebuildCacheAsync("catalog signature changed");
    }

    private bool IsCacheValid(ScripShopCatalogCacheDocument cacheDocument)
        => cacheDocument.Items.Count > 0
           && string.Equals(cacheDocument.CatalogVersion, _catalogBuilder.GetCatalogVersion(), StringComparison.Ordinal);

    private async Task<ScripShopCatalogCacheDocument> ReadCacheAsync()
    {
        await using var stream = File.OpenRead(CachePath);
        return await JsonSerializer.DeserializeAsync<ScripShopCatalogCacheDocument>(stream) ?? new ScripShopCatalogCacheDocument();
    }

    private async Task WriteCacheAsync(List<ScripShopItem> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath) ?? Svc.PluginInterface.ConfigDirectory.FullName);
        var cacheDocument = new ScripShopCatalogCacheDocument
        {
            CatalogVersion = _catalogBuilder.GetCatalogVersion(),
            GeneratedAtUtc = DateTime.UtcNow,
            Items = items,
        };

        await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(cacheDocument, JsonOptions));
    }

    private void SyncConfiguredItems(List<ScripShopItem> latestItems)
    {
        if (_config.ScripShopItems.Count == 0 || latestItems.Count == 0)
            return;

        var latestLookup = latestItems.ToDictionary(item => item.ItemId);
        var changed = false;

        foreach (var configuredItem in _config.ScripShopItems)
        {
            if (!latestLookup.TryGetValue(configuredItem.Item.ItemId, out var latest))
                continue;

            if (AreEquivalent(configuredItem.Item, latest))
                continue;

            configuredItem.Item = CloneItem(latest);
            changed = true;
        }

        if (changed)
            _configurationStore.Save();
    }

    private static bool AreEquivalent(ScripShopItem left, ScripShopItem right)
        => left.ItemId == right.ItemId
           && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
           && left.Index == right.Index
           && left.ItemCost == right.ItemCost
           && left.Page == right.Page
           && left.SubPage == right.SubPage
           && left.CurrencySpecialId == right.CurrencySpecialId
           && left.CurrencyItemId == right.CurrencyItemId
           && string.Equals(left.CurrencyName, right.CurrencyName, StringComparison.Ordinal)
           && left.Discipline == right.Discipline
           && left.TierRank == right.TierRank;

    private static ScripShopItem CloneItem(ScripShopItem item)
        => new()
        {
            Name = item.Name,
            ItemID = item.ItemID,
            Index = item.Index,
            ItemCost = item.ItemCost,
            Page = item.Page,
            SubPage = item.SubPage,
            CurrencySpecialId = item.CurrencySpecialId,
            CurrencyItemId = item.CurrencyItemId,
            CurrencyName = item.CurrencyName,
            Discipline = item.Discipline,
            TierRank = item.TierRank,
        };
}
