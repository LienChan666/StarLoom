using StarLoom.Ui;
using System.IO;
using Xunit;

namespace StarLoom.Tests.Ui;

public sealed class UiTextTests
{
    [Fact]
    public void Get_Should_Load_English_Text_From_Resources()
    {
        var uiText = UiText.CreateForTests(AppContext.BaseDirectory, "en");

        Assert.Equal("Start", uiText.Get("common.start"));
    }

    [Fact]
    public void Reload_Should_Keep_Previous_Translations_When_Localization_File_Becomes_Invalid()
    {
        using var scope = LocalizationTestScope.Create("en", """
            {
              "common.start": "Start"
            }
            """);
        var uiText = UiText.CreateForTests(scope.BaseDirectory, "en");

        scope.WriteLanguageFile("en", "{");

        var exception = Record.Exception(uiText.Reload);

        Assert.Null(exception);
        Assert.Equal("Start", uiText.Get("common.start"));
    }

    [Fact]
    public void CreateForTests_Should_Not_Throw_When_Localization_File_Is_Missing_On_Startup()
    {
        using var scope = LocalizationTestScope.Create("en", """
            {
              "common.start": "Start"
            }
            """);
        scope.DeleteLanguageFile("en");

        var exception = Record.Exception(() => UiText.CreateForTests(scope.BaseDirectory, "en"));

        Assert.Null(exception);
    }

    private sealed class LocalizationTestScope : IDisposable
    {
        private readonly string directoryPath;

        private LocalizationTestScope(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public string BaseDirectory => directoryPath;

        public static LocalizationTestScope Create(string language, string content)
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "StarLoom.UiTextTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directoryPath, "Resources", "Localization"));

            var scope = new LocalizationTestScope(directoryPath);
            scope.WriteLanguageFile(language, content);
            return scope;
        }

        public void WriteLanguageFile(string language, string content)
        {
            File.WriteAllText(GetLanguageFilePath(language), content);
        }

        public void DeleteLanguageFile(string language)
        {
            var filePath = GetLanguageFilePath(language);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, recursive: true);
        }

        private string GetLanguageFilePath(string language)
        {
            return Path.Combine(directoryPath, "Resources", "Localization", $"{language}.json");
        }
    }
}
