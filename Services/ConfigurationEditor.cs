using Starloom.Data;
using System;
using System.Collections.Generic;

namespace Starloom.Services;

public sealed class ConfigurationEditor
{
    private readonly ConfigurationStore configurationStore;

    public ConfigurationEditor(ConfigurationStore configurationStore)
    {
        this.configurationStore = configurationStore;
    }

    public void SetUiLanguage(string language)
        => Update(configuration =>
        {
            if (string.Equals(configuration.UiLanguage, language, StringComparison.Ordinal))
                return false;

            configuration.UiLanguage = language;
            return true;
        });

    public void SetArtisanListId(int artisanListId)
        => Update(configuration =>
        {
            var normalized = Math.Max(0, artisanListId);
            if (configuration.ArtisanListId == normalized)
                return false;

            configuration.ArtisanListId = normalized;
            return true;
        });

    public void SetPreferredCollectableShop(CollectableShop shop)
        => Update(configuration =>
        {
            if (configuration.PreferredCollectableShop != null
                && string.Equals(configuration.PreferredCollectableShop.Name, shop.Name, StringComparison.Ordinal))
            {
                return false;
            }

            configuration.PreferredCollectableShop = shop;
            return true;
        });

    public void SetDefaultCraftReturnPoint(HousingReturnPoint point)
        => Update(configuration =>
        {
            var current = configuration.DefaultCraftReturnPoint;
            if (current != null
                && current.IsInn == point.IsInn
                && current.AetheryteId == point.AetheryteId
                && current.SubIndex == point.SubIndex
                && current.TerritoryId == point.TerritoryId
                && current.IsApartment == point.IsApartment
                && string.Equals(current.DisplayName, point.DisplayName, StringComparison.Ordinal))
            {
                return false;
            }

            configuration.DefaultCraftReturnPoint = ClonePoint(point);
            return true;
        });

    public void SetPostPurchaseAction(PurchaseCompletionAction action)
        => Update(configuration =>
        {
            if (configuration.PostPurchaseAction == action)
                return false;

            configuration.PostPurchaseAction = action;
            return true;
        });

    public void SetReserveScripAmount(int amount)
        => Update(configuration =>
        {
            var normalized = Math.Max(0, amount);
            if (configuration.ReserveScripAmount == normalized)
                return false;

            configuration.ReserveScripAmount = normalized;
            return true;
        });

    public void SetFreeSlotThreshold(int threshold)
        => Update(configuration =>
        {
            var normalized = Math.Max(0, threshold);
            if (configuration.FreeSlotThreshold == normalized)
                return false;

            configuration.FreeSlotThreshold = normalized;
            return true;
        });

    public void AddPurchaseItem(ScripShopItem item)
        => Update(configuration =>
        {
            if (configuration.ScripShopItems.Exists(existing => existing.Item.ItemId == item.ItemId))
                return false;

            configuration.ScripShopItems.Add(new ItemToPurchase
            {
                Item = CloneItem(item),
                Quantity = 1,
            });
            return true;
        });

    public void SetPurchaseItemQuantity(int index, int quantity)
        => Update(configuration =>
        {
            if (index < 0 || index >= configuration.ScripShopItems.Count)
                return false;

            var normalized = Math.Max(1, quantity);
            if (configuration.ScripShopItems[index].Quantity == normalized)
                return false;

            configuration.ScripShopItems[index].Quantity = normalized;
            return true;
        });

    public void RemovePurchaseItemAt(int index)
        => Update(configuration =>
        {
            if (index < 0 || index >= configuration.ScripShopItems.Count)
                return false;

            configuration.ScripShopItems.RemoveAt(index);
            return true;
        });

    public void MovePurchaseItem(int fromIndex, int toIndex)
        => Update(configuration =>
        {
            if (fromIndex < 0
                || fromIndex >= configuration.ScripShopItems.Count
                || toIndex < 0
                || toIndex >= configuration.ScripShopItems.Count
                || fromIndex == toIndex)
            {
                return false;
            }

            var list = configuration.ScripShopItems;
            (list[fromIndex], list[toIndex]) = (list[toIndex], list[fromIndex]);
            return true;
        });

    public void SyncConfiguredItems(IReadOnlyList<ScripShopItem> latestItems)
        => Update(configuration =>
        {
            if (configuration.ScripShopItems.Count == 0 || latestItems.Count == 0)
                return false;

            var latestLookup = new Dictionary<uint, ScripShopItem>();
            foreach (var item in latestItems)
                latestLookup[item.ItemId] = item;

            var changed = false;
            foreach (var configuredItem in configuration.ScripShopItems)
            {
                if (!latestLookup.TryGetValue(configuredItem.Item.ItemId, out var latest))
                    continue;

                if (AreEquivalent(configuredItem.Item, latest))
                    continue;

                configuredItem.Item = CloneItem(latest);
                changed = true;
            }

            return changed;
        });

    private void Update(Func<Configuration, bool> mutate)
    {
        if (!mutate(configurationStore.Configuration))
            return;

        configurationStore.Save();
    }

    private static bool AreEquivalent(ScripShopItem left, ScripShopItem right)
        => left.ItemId == right.ItemId
           && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
           && left.Index == right.Index
           && left.ItemCost == right.ItemCost
           && left.ItemIconId == right.ItemIconId
           && left.Page == right.Page
           && left.SubPage == right.SubPage
           && left.CurrencySpecialId == right.CurrencySpecialId
           && left.CurrencyItemId == right.CurrencyItemId
           && string.Equals(left.CurrencyName, right.CurrencyName, StringComparison.Ordinal)
           && left.CurrencyIconId == right.CurrencyIconId
           && left.Discipline == right.Discipline
           && left.TierRank == right.TierRank;

    private static ScripShopItem CloneItem(ScripShopItem item)
        => new()
        {
            Name = item.Name,
            ItemID = item.ItemID,
            Index = item.Index,
            ItemCost = item.ItemCost,
            ItemIconId = item.ItemIconId,
            Page = item.Page,
            SubPage = item.SubPage,
            CurrencySpecialId = item.CurrencySpecialId,
            CurrencyItemId = item.CurrencyItemId,
            CurrencyName = item.CurrencyName,
            CurrencyIconId = item.CurrencyIconId,
            Discipline = item.Discipline,
            TierRank = item.TierRank,
        };

    private static HousingReturnPoint ClonePoint(HousingReturnPoint point)
        => new()
        {
            AetheryteId = point.AetheryteId,
            SubIndex = point.SubIndex,
            TerritoryId = point.TerritoryId,
            IsInn = point.IsInn,
            IsApartment = point.IsApartment,
            DisplayName = point.DisplayName,
        };
}
