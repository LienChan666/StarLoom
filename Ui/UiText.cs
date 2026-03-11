using StarLoom.Config;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace StarLoom.Ui;

public sealed class UiText
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Func<string> getLanguage;
    private readonly string baseDirectory;
    private Dictionary<string, string> translations = new(StringComparer.Ordinal);

    public UiText(ConfigStore configStore)
        : this(
            () => configStore.pluginConfig.uiLanguage,
            Svc.PluginInterface.AssemblyLocation.DirectoryName ?? AppContext.BaseDirectory)
    {
    }

    private UiText(Func<string> getLanguage, string baseDirectory)
    {
        this.getLanguage = getLanguage;
        this.baseDirectory = baseDirectory;
        Reload();
    }

    internal static UiText CreateForTests(string baseDirectory, string language)
    {
        return new UiText(() => language, baseDirectory);
    }

    public void Reload()
    {
        var filePath = Path.Combine(baseDirectory, "Resources", "Localization", $"{getLanguage()}.json");
        var json = File.ReadAllText(filePath);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        if (parsed is null || parsed.Count == 0)
            throw new InvalidOperationException($"Localization file is empty: {filePath}");

        translations = parsed;
    }

    public string Get(string key)
    {
        return translations[key];
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, Get(key), args);
    }
}
