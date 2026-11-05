using ShareVibeX.Core.Localization;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Tests;

[CollectionDefinition("Localization state", DisableParallelization = true)]
public sealed class LocalizationStateCollection;

[Collection("Localization state")]
public sealed class LegacySettingsLanguageTests
{
    [Theory]
    [InlineData(AppLanguage.English, "\"Amoled\"")]
    [InlineData(AppLanguage.Russian, "\"Amoled\"")]
    [InlineData(AppLanguage.English, "2")]
    [InlineData(AppLanguage.Russian, "2")]
    public async Task MigrationUsesStableJsonKeysRegardlessOfDisplayLanguage(AppLanguage language, string theme)
    {
        var previous = Loc.Language;
        var root = Path.Combine(Path.GetTempPath(), "ShareVibeX-legacy-language-" + Guid.NewGuid().ToString("N"));
        try
        {
            Loc.Language = language;
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "settings.json");
            var original = $"{{\"SchemaVersion\":1,\"Theme\":{theme},\"CaptureHotkey\":\"Alt+F8\"}}";
            await File.WriteAllTextAsync(path, original);
            var store = new PortableSettingsStore(root);
            var settings = await store.LoadAsync();
            Assert.Equal(ThemeMode.Contrast, settings.Theme);
            Assert.Equal("Alt+F8", settings.CaptureHotkey);
            Assert.Equal(original, await File.ReadAllTextAsync(path));
            await store.SaveAsync(settings);
            using var saved = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal("Contrast", saved.RootElement.GetProperty("Theme").GetString());
            Assert.False(saved.RootElement.TryGetProperty("Тема", out _));
        }
        finally
        {
            Loc.Language = previous;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
