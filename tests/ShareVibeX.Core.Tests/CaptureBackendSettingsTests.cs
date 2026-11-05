using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Tests;

public sealed class CaptureBackendSettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ShareVibeX-backend-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public async Task MissingFieldDefaultsToAutomaticWithoutRewritingFile()
    {
        Directory.CreateDirectory(root);
        const string json = "{\"SchemaVersion\":2,\"CaptureHotkey\":\"Alt+F8\",\"HistoryEnabled\":true}";
        var path = Path.Combine(root, "settings.json");
        await File.WriteAllTextAsync(path, json);
        var settings = await new PortableSettingsStore(root).LoadAsync();
        Assert.Equal(CaptureBackend.Automatic, settings.CaptureBackend);
        Assert.Equal("Alt+F8", settings.CaptureHotkey);
        Assert.True(settings.HistoryEnabled);
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData(CaptureBackend.Automatic)]
    [InlineData(CaptureBackend.WindowsGraphicsCapture)]
    [InlineData(CaptureBackend.Gdi)]
    public async Task BackendRoundTripsWithUnrelatedSettings(CaptureBackend backend)
    {
        var store = new PortableSettingsStore(root);
        var settings = new AppSettings(CaptureBackend: backend, CaptureHotkey: "Alt+F8", HistoryEnabled: true);
        await store.SaveAsync(settings);
        Assert.Equal(settings, await store.LoadAsync());
    }

    [Fact]
    public async Task InvalidSaveDoesNotReplaceExistingSettings()
    {
        var store = new PortableSettingsStore(root);
        await store.SaveAsync(new AppSettings());
        var path = Path.Combine(root, "settings.json");
        var original = await File.ReadAllTextAsync(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new AppSettings(CaptureBackend: (CaptureBackend)99)));
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("99")]
    [InlineData("\"Unknown\"")]
    public async Task InvalidLoadPreservesFile(string value)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "settings.json");
        var json = "{\"SchemaVersion\":2,\"CaptureBackend\":" + value + "}";
        await File.WriteAllTextAsync(path, json);
        Assert.NotNull(await Record.ExceptionAsync(() => new PortableSettingsStore(root).LoadAsync()));
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }
}
