using ShareVibeX.App.Recording;

namespace ShareVibeX.App.Tests;

public sealed class FfmpegLocatorTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ShareVibeX-path-test"));
    private static string Name => OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
    [Fact]
    public void ExplicitRelativePathIsPortableAndInvalidOverrideDoesNotFallBack()
    {
        var expected = Path.Combine(Root, "custom", Name);
        Assert.Equal(expected, FfmpegLocator.Resolve(Path.Combine("custom", Name), Root, "", path => path == expected));
        Assert.Throws<FileNotFoundException>(() => FfmpegLocator.Resolve("missing.exe", Root, "", _ => false));
    }
    [Fact]
    public void SystemPathPrecedesBundledTool()
    {
        var system = Path.Combine(Root, "installed", Name);
        var bundled = Path.Combine(Root, "tools", Name);
        var result = FfmpegLocator.Resolve(null, Root, Path.GetDirectoryName(system), path => path == system || path == bundled);
        Assert.Equal(system, result);
    }
    [Fact]
    public void BundledToolIsFallback()
    {
        var bundled = Path.Combine(Root, "tools", Name);
        Assert.Equal(bundled, FfmpegLocator.Resolve(null, Root, "", path => path == bundled));
    }
}
