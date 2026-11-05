using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class BitmapExportTests(UiTestFixture ui) : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ShareVibeX-export", Guid.NewGuid().ToString("N"));
    private readonly BitmapExportService service = new();

    private static DesktopSnapshot Fixture() => new(new(-2,-1,3,2), [new("screen",new(-2,-1,3,2),1)],
        new(3,2,12,[1,2,3,255, 4,5,6,255, 7,8,9,255, 10,11,12,255, 13,14,15,255, 16,17,18,255]));

    [Fact]
    public Task NegativeOriginCropPreservesExactPixels() => ui.Run(() =>
    {
        var crop = service.Crop(Fixture(),new(-1,-1,2,2));
        Assert.Equal(2,crop.Width); Assert.Equal(2,crop.Height); Assert.Equal(8,crop.Stride);
        Assert.Equal(new byte[] {4,5,6,255,7,8,9,255,13,14,15,255,16,17,18,255},crop.Bgra32);
    });

    [Fact]
    public void OutOfBoundsCropIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => service.Crop(Fixture(),new(-3,-1,2,2)));

    [Fact]
    public Task PngRoundTripPreservesDimensionsAndColorAtUnicodePath() => ui.Run(async () =>
    {
        var path = Path.Combine(directory,"日本語 image.png");
        var frame = service.Crop(Fixture(),new(-1,-1,2,2));
        await service.SavePngAsync(frame,path);
        using var decoded = new Bitmap(path);
        Assert.Equal(new PixelSize(2,2),decoded.PixelSize);
        var handle = Marshal.AllocHGlobal(16);
        try
        {
            decoded.CopyPixels(new Avalonia.PixelRect(0,0,2,2),handle,16,8);
            var pixels = new byte[16]; Marshal.Copy(handle,pixels,0,16);
            // Skia decoding may use RGBA; compare channels using the actual declared format.
            if (decoded.Format == Avalonia.Platform.PixelFormat.Rgba8888)
                for (var i=0;i<pixels.Length;i+=4) (pixels[i],pixels[i+2])=(pixels[i+2],pixels[i]);
            Assert.Equal(frame.Bgra32,pixels);
        }
        finally { Marshal.FreeHGlobal(handle); }
        Assert.Single(Directory.GetFiles(directory));
    });

    [Fact]
    public Task CancelledSavePreservesDestination() => ui.Run(async () =>
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory,"existing.png"); await File.WriteAllTextAsync(path,"original");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SavePngAsync(Fixture().Frame,path,cancelled.Token));
        Assert.Equal("original",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(directory));
    });

    [Fact]
    public Task FailedSavePreservesDestinationAndRemovesTempFile() => ui.Run(async () =>
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory,"existing.png"); await File.WriteAllTextAsync(path,"original");
        using (var locked = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            var error = await Record.ExceptionAsync(() => service.SavePngAsync(Fixture().Frame,path));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("original",await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(directory));
    });

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
}
