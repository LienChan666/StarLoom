using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Starloom.Data;
using System.Collections.Generic;
using System.Numerics;

namespace Starloom.UI.Components.Shared;

internal static class ScripShopUiHelpers
{
    private static readonly Dictionary<uint, ISharedImmediateTexture> IconCache = [];

    internal static void DrawItemLabel(ScripShopItem item)
        => DrawIconText(item.Name, item.ItemIconId);

    internal static void DrawCurrencyLabel(ScripShopItem item)
    {
        DrawIconText(GetCurrencyLabel(item), item.CurrencyIconId);
        if (!string.IsNullOrWhiteSpace(item.CurrencyName) && ImGui.IsItemHovered())
            ImGui.SetTooltip(item.CurrencyName);
    }

    internal static string GetCurrencyLabel(ScripShopItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.CurrencyName))
            return item.CurrencyName;

        if (item.Discipline == ScripDiscipline.Crafting)
            return P.Localization.Get("currency.crafting");

        return item.Discipline == ScripDiscipline.Gathering
            ? P.Localization.Get("currency.gathering")
            : P.Localization.Get("currency.unknown");
    }

    private static void DrawIconText(string text, uint iconId)
    {
        const float iconSpacing = 6f;
        var iconSize = new Vector2(ImGui.GetTextLineHeight());

        if (TryGetIconWrap(iconId, out var wrap))
        {
            ImGui.Image(wrap.Handle, iconSize);
            ImGui.SameLine(0f, iconSpacing);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);
    }

    private static bool TryGetIconWrap(uint iconId, out IDalamudTextureWrap wrap)
    {
        wrap = null!;
        if (iconId == 0)
            return false;

        if (!IconCache.TryGetValue(iconId, out var texture))
        {
            texture = P.Textures.GetFromGameIcon(new GameIconLookup(iconId));
            IconCache[iconId] = texture;
        }

        if (texture is not { } loadedTexture)
            return false;

        if (!loadedTexture.TryGetWrap(out var textureWrap, out _) || textureWrap == null)
            return false;

        wrap = textureWrap;
        return true;
    }
}
