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

    public static List<ScripShopItem> ShopItems = [];
    public static bool IsLoading { get; private set; }
    public bool IsRefreshing { get; private set; }
    public string StatusMessage { get; private set; } = "工票物品索引尚未加载。";
    public string CacheFilePath => Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, CacheFileName);

    public ScripShopItemManager(Configuration config, ConfigurationStore configurationStore)
    {
        _config = config;
        _configurationStore = configurationStore;
        RequestLoad();
    }

    public void RequestLoad()
        => _ = Task.Run(LoadScripItemsAsync);

    public void RequestRefresh()
    {
        if (IsLoading)
            return;

        _ = Task.Run(RefreshCacheAsync);
    }

    public async Task LoadScripItemsAsync()
    {
        await _syncLock.WaitAsync();
        IsLoading = true;
        try
        {
            var items = await EnsureCacheAndLoadAsync();
            ShopItems = items;
            SyncConfiguredItems(items);
            StatusMessage = $"已加载 {items.Count} 条工票物品。";
        }
        catch (Exception ex)
        {
            ShopItems = [];
            StatusMessage = $"工票物品索引加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _syncLock.Release();
        }
    }

    public async Task RefreshCacheAsync()
    {
        await _syncLock.WaitAsync();
        IsLoading = true;
        IsRefreshing = true;
        var previousItems = ShopItems;
        try
        {
            StatusMessage = "正在构建工票物品列表...";
            var items = _catalogBuilder.BuildCatalog();
            await WriteCacheAsync(items);
            ShopItems = items;
            SyncConfiguredItems(items);
            StatusMessage = $"已刷新工票物品列表，共 {items.Count} 条。";
        }
        catch (Exception ex)
        {
            ShopItems = previousItems;
            StatusMessage = $"刷新工票物品列表失败：{ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
            IsLoading = false;
            _syncLock.Release();
        }
    }

    private async Task<List<ScripShopItem>> EnsureCacheAndLoadAsync()
    {
        if (!File.Exists(CacheFilePath))
        {
            StatusMessage = "未找到外部工票物品列表，正在自动构建...";
            var generatedItems = _catalogBuilder.BuildCatalog();
            await WriteCacheAsync(generatedItems);
            return generatedItems;
        }

        ScripShopCatalogCacheDocument cacheDocument;
        try
        {
            cacheDocument = await ReadCacheAsync();
        }
        catch (Exception)
        {
            cacheDocument = new ScripShopCatalogCacheDocument();
        }

        if (IsCacheValid(cacheDocument))
            return cacheDocument.Items;

        StatusMessage = "工票物品缓存已过期，正在重新构建...";
        var rebuiltItems = _catalogBuilder.BuildCatalog();
        await WriteCacheAsync(rebuiltItems);
        return rebuiltItems;
    }

    private bool IsCacheValid(ScripShopCatalogCacheDocument cacheDocument)
        => cacheDocument.Items.Count > 0
           && string.Equals(cacheDocument.CatalogVersion, _catalogBuilder.GetCatalogVersion(), StringComparison.Ordinal);

    private async Task<ScripShopCatalogCacheDocument> ReadCacheAsync()
    {
        await using var stream = File.OpenRead(CacheFilePath);
        return await JsonSerializer.DeserializeAsync<ScripShopCatalogCacheDocument>(stream) ?? new ScripShopCatalogCacheDocument();
    }

    private async Task WriteCacheAsync(List<ScripShopItem> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath) ?? Svc.PluginInterface.ConfigDirectory.FullName);
        var cacheDocument = new ScripShopCatalogCacheDocument
        {
            CatalogVersion = _catalogBuilder.GetCatalogVersion(),
            GeneratedAtUtc = DateTime.UtcNow,
            Items = items,
        };

        await File.WriteAllTextAsync(CacheFilePath, JsonSerializer.Serialize(cacheDocument, JsonOptions));
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
