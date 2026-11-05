using System.Text.Json;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Core.Tests;

public sealed class PortableSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ShareVibeX-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(directory, "settings.json");

    [Fact]
    public async Task MissingFileLoadsDefaultsWithoutWriting()
    {
        var settings = await new PortableSettingsStore(directory).LoadAsync();
        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.Equal("PrintScreen",settings.CaptureHotkey);
        Assert.Equal(ShareVibeX.Core.Capture.CaptureBackend.Gdi,settings.CaptureBackend);
        Assert.True(settings.SelectionDefaults.DetectWindows);
        Assert.True(settings.SelectionDefaults.DetectControls);
        Assert.True(settings.SelectionDefaults.RestrictToActiveMonitor);
        Assert.Equal(2,settings.SelectionDefaults.OutlineThickness);
        Assert.Null(settings.OutputDirectory);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task RootSettingsTakePriorityAndKeepTheLegacyFile()
    {
        var settingsPath=Path.Combine(directory,"settings.json");
        var legacy=new PortableSettingsStore(Path.Combine(directory,"data"));
        await legacy.SaveAsync(new AppSettings(CaptureHotkey:"Alt+F8"));
        var portable=new PortableSettingsStore(legacy.DataDirectory,settingsPath);
        Assert.Equal("Alt+F8",(await portable.LoadAsync()).CaptureHotkey);
        Assert.Equal(legacy.SettingsPath,portable.ExistingSettingsPath);
        Assert.False(File.Exists(settingsPath));
        await portable.SaveAsync(AppSettings.FactoryDefaults);
        Assert.Equal(settingsPath,portable.ExistingSettingsPath);
        Assert.Equal("PrintScreen",(await portable.LoadAsync()).CaptureHotkey);
        Assert.Equal("Alt+F8",(await legacy.LoadAsync()).CaptureHotkey);
    }

    [Fact]
    public async Task ThemeAndUnicodeOutputRoundTrip()
    {
        var store = new PortableSettingsStore(directory);
        await store.SaveAsync(new AppSettings(Theme: ThemeMode.Contrast, OutputDirectory: "C:/Screens/日本語 images"));
        var result = await store.LoadAsync();
        Assert.Equal(ThemeMode.Contrast, result.Theme);
        Assert.Equal("C:/Screens/日本語 images", result.OutputDirectory);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData("{bad", typeof(JsonException))]
    [InlineData("{\"SchemaVersion\":99}", typeof(InvalidDataException))]
    [InlineData("null", typeof(InvalidDataException))]
    [InlineData("{\"SchemaVersion\":1,\"Theme\":999}", typeof(InvalidDataException))]
    public async Task InvalidSettingsArePreserved(string original, Type exception)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(FilePath, original);
        if(exception==typeof(JsonException))await Assert.ThrowsAnyAsync<JsonException>(()=>new PortableSettingsStore(directory).LoadAsync());
        else await Assert.ThrowsAsync(exception, () => new PortableSettingsStore(directory).LoadAsync());
        Assert.Equal(original, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task FailedReplacementPreservesExistingSettingsAndCleansTempFile()
    {
        var store = new PortableSettingsStore(directory);
        await store.SaveAsync(new AppSettings());
        var original = await File.ReadAllBytesAsync(FilePath);
        using (var locked = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = await Record.ExceptionAsync(() => store.SaveAsync(new AppSettings(Theme: ThemeMode.Contrast)));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(original, await File.ReadAllBytesAsync(FilePath));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task CancelledSaveDoesNotReplaceExistingSettings()
    {
        var store = new PortableSettingsStore(directory);
        await store.SaveAsync(new AppSettings());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new AppSettings(Theme: ThemeMode.Contrast), cancellation.Token));
        Assert.Equal(ThemeMode.System, (await store.LoadAsync()).Theme);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Theory]
    [InlineData("\"Light\"",ThemeMode.System)]
    [InlineData("\"Dark\"",ThemeMode.System)]
    [InlineData("\"Amoled\"",ThemeMode.Contrast)]
    [InlineData("2",ThemeMode.Contrast)]
    public async Task LegacyThemesMigrateWithoutLosingCaptureSettings(string theme,ThemeMode expected)
    {
        Directory.CreateDirectory(directory);
        var json=$"{{\"SchemaVersion\":1,\"Theme\":{theme},\"CaptureHotkey\":\"Ctrl+F9\",\"OutputDirectory\":\"C:/Screens\"}}";
        await File.WriteAllTextAsync(FilePath,json);
        var settings=await new PortableSettingsStore(directory).LoadAsync();
        Assert.Equal(2,settings.SchemaVersion);Assert.Equal(expected,settings.Theme);
        Assert.Equal("Ctrl+F9",settings.CaptureHotkey);Assert.Equal("C:/Screens",settings.OutputDirectory);
        Assert.Equal(ImageFormat.Png,settings.ImageFormat);Assert.Equal(90,settings.ImageQuality);
        Assert.Equal(json,await File.ReadAllTextAsync(FilePath));
    }
    [Fact]
    public async Task ImageFormatAndQualityRoundTrip()
    {
        var store=new PortableSettingsStore(directory);await store.SaveAsync(new AppSettings(ImageFormat:ImageFormat.Webp,ImageQuality:75));
        var settings=await store.LoadAsync();Assert.Equal(ImageFormat.Webp,settings.ImageFormat);Assert.Equal(75,settings.ImageQuality);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task InvalidImageQualityIsRejected(int quality)=>await Assert.ThrowsAsync<InvalidDataException>(()=>new PortableSettingsStore(directory).SaveAsync(new AppSettings(ImageQuality:quality)));

    [Fact]
    public async Task ExistingSchemaTwoGainsRecordingDefaultsWithoutLosingOtherOptions()
    {
        Directory.CreateDirectory(directory);
        var json = "{\"SchemaVersion\":2,\"Theme\":\"Contrast\",\"CaptureHotkey\":\"Alt+F8\",\"ImageFormat\":\"Webp\",\"ImageQuality\":73}";
        await File.WriteAllTextAsync(FilePath, json);
        var settings = await new PortableSettingsStore(directory).LoadAsync();
        Assert.Equal("Alt+F8", settings.CaptureHotkey); Assert.Equal(ThemeMode.Contrast, settings.Theme);
        Assert.Equal(ImageFormat.Webp, settings.ImageFormat); Assert.Equal(73, settings.ImageQuality);
        Assert.Equal(30, settings.RecordingDefaults.FrameRate); Assert.Equal(AudioSource.System, settings.RecordingDefaults.AudioSource);
        Assert.Equal(json, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task RecordingOptionsRoundTripAndInvalidOptionsDoNotReplaceExistingSettings()
    {
        var store = new PortableSettingsStore(directory);
        var recording = new RecordingOptions { ScaleMode = ScaleMode.DivideBy, DivideBy = 1.5, FrameRate = 60, FfmpegPath = @"tools\ffmpeg.exe" };
        await store.SaveAsync(new(Recording: recording));
        Assert.Equal(recording, (await store.LoadAsync()).RecordingDefaults);
        var original = await File.ReadAllBytesAsync(FilePath);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new(Recording: recording with { FrameRate = 0 })));
        Assert.Equal(original, await File.ReadAllBytesAsync(FilePath));
    }
}
