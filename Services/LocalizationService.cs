using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Starloom.Services;

public sealed class LocalizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ConfigurationStore configurationStore;
    private Dictionary<string, string> translations = new(StringComparer.Ordinal);

    public LocalizationService(ConfigurationStore configurationStore)
    {
        this.configurationStore = configurationStore;
        Reload();
    }

    public string CurrentLanguage => configurationStore.Configuration.UiLanguage;

    public void Reload()
    {
        var filePath = GetFilePath(CurrentLanguage);
        try
        {
            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (parsed is null or { Count: 0 })
            {
                Svc.Log.Warning($"Localization file was empty or invalid: {filePath}");
                return;
            }

            translations = parsed;
            Svc.Log.Info($"Loaded UI localization: {CurrentLanguage}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to load localization file: {filePath}\n{ex}");
        }
    }

    public string Get(string key)
        => translations[key];

    public string Format(string key, params object[] args)
        => string.Format(CultureInfo.InvariantCulture, Get(key), args);

    private static string GetFilePath(string language)
    {
        var baseDirectory = Svc.PluginInterface.AssemblyLocation.DirectoryName ?? AppContext.BaseDirectory;
        return Path.Combine(baseDirectory, "Resources", "Localization", $"{language}.json");
    }
}
