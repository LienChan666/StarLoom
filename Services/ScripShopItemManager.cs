using ECommons.DalamudServices;
using Starloom.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starloom.Services;

public sealed class ScripShopItemManager
{
    private const string CacheFileName = "ScripShopItems.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Configuration config;
    private readonly ConfigurationEditor configurationEditor;
    private readonly ScripShopCatalogBuilder catalogBuilder = new();
    private readonly SemaphoreSlim syncLock = new(1, 1);
    private string CachePath => Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, CacheFileName);

    internal List<ScripShopItem> ShopItems = [];
    internal bool IsLoading { get; private set; }

    public ScripShopItemManager(Configuration config, ConfigurationEditor configurationEditor)
    {
        this.config = config;
        this.configurationEditor = configurationEditor;
        RequestLoad();
    }

    public void RequestLoad()
        => _ = Task.Run(LoadScripItemsAsync);

    public async Task LoadScripItemsAsync()
    {
        await syncLock.WaitAsync();
        IsLoading = true;
        try
        {
            var items = await EnsureCacheAndLoadAsync();
            ShopItems = items;
            SyncConfiguredItems(items);
            Svc.Log.Info($"Catalog loaded: {items.Count} items.");
        }
        catch (Exception ex)
        {
            ShopItems = [];
            Svc.Log.Error($"Catalog load failed: {ex}");
        }
        finally
        {
            IsLoading = false;
            syncLock.Release();
        }
    }

    private async Task<List<ScripShopItem>> RebuildCacheAsync(string reason)
    {
        Svc.Log.Info($"Rebuilding catalog cache: {reason}.");
        var items = catalogBuilder.BuildCatalog();
        await WriteCacheAsync(items);
        Svc.Log.Info($"Catalog rebuilt: {items.Count} items.");
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
           && string.Equals(cacheDocument.CatalogVersion, catalogBuilder.GetCatalogVersion(), StringComparison.Ordinal);

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
            CatalogVersion = catalogBuilder.GetCatalogVersion(),
            GeneratedAtUtc = DateTime.UtcNow,
            Items = items,
        };

        await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(cacheDocument, JsonOptions));
    }

    private void SyncConfiguredItems(List<ScripShopItem> latestItems)
    {
        configurationEditor.SyncConfiguredItems(latestItems);
    }
}
