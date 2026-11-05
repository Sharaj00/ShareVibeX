using ShareVibeX.App.Recording;
using ShareVibeX.Core.Localization;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

public sealed class PortableRelocationTests
{
    [Fact]
    public async Task MovingPortableFolderPreservesSettingsAndResolvesRelativeToolsFromNewLocation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ShareVibeX-relocation-" + Guid.NewGuid().ToString("N"));
        var before = Path.Combine(root, "original");
        var after = Path.Combine(root, "Moved 日本語 copy");
        var toolName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var relativeTool = Path.Combine("tools", "ffmpeg", "bin", toolName);
        try
        {
            var tool = Path.Combine(before, relativeTool);
            Directory.CreateDirectory(Path.GetDirectoryName(tool)!);
            // Discovery fixture only: never execute this file or inspect real system tools.
            await File.WriteAllTextAsync(tool, "synthetic executable placeholder");
            var expected = new AppSettings(Language: AppLanguage.Russian, CaptureHotkey: "Alt+F8", ImageFormat: ImageFormat.Webp,
                Recording: new RecordingOptions { FfmpegPath = relativeTool, FrameRate = 60 });
            await new PortableSettingsStore(Path.Combine(before, "data")).SaveAsync(expected);
            Directory.Move(before, after);
            var loaded = await new PortableSettingsStore(Path.Combine(after, "data")).LoadAsync();
            Assert.Equal(expected, loaded);
            var relocatedTool = Path.Combine(after, relativeTool);
            Assert.Equal(relocatedTool, FfmpegLocator.Resolve(loaded.RecordingDefaults.FfmpegPath, after, ""));
            Assert.Equal(relocatedTool, FfmpegLocator.Resolve(null, after, "", path => path == relocatedTool && File.Exists(path)));
            Assert.False(Directory.Exists(before));
            File.Delete(relocatedTool);
            Assert.Throws<FileNotFoundException>(() => FfmpegLocator.Resolve(loaded.RecordingDefaults.FfmpegPath, after, "", _ => false));
            // Removing an optional video dependency must not invalidate screenshot preferences.
            Assert.Equal(expected, await new PortableSettingsStore(Path.Combine(after, "data")).LoadAsync());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
