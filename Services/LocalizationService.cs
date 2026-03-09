using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace StarLoom.Services;

public sealed class LocalizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ConfigurationStore _configurationStore;
    private Dictionary<string, string> _translations = new(StringComparer.Ordinal);

    public LocalizationService(ConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
        Reload();
    }

    public string CurrentLanguage => _configurationStore.Configuration.UiLanguage;

    public void Reload()
    {
        var filePath = GetFilePath(CurrentLanguage);
        var json = File.ReadAllText(filePath);
        _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load localization file: {filePath}");

        Svc.Log.Info($"[Starloom] Loaded UI localization: {CurrentLanguage}");
    }

    public string Get(string key)
        => _translations[key];

    public string Format(string key, params object[] args)
        => string.Format(CultureInfo.InvariantCulture, Get(key), args);

    private static string GetFilePath(string language)
    {
        var baseDirectory = Svc.PluginInterface.AssemblyLocation.DirectoryName ?? AppContext.BaseDirectory;
        return Path.Combine(baseDirectory, "Resources", "Localization", $"{language}.json");
    }
}
