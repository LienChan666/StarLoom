using System;
using System.Collections.Generic;

namespace StarLoom.Data;

public sealed class ScripShopCatalogCacheDocument
{
    public string CatalogVersion { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public List<ScripShopItem> Items { get; set; } = [];
}
