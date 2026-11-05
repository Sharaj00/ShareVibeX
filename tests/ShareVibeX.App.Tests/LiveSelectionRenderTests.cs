using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class LiveSelectionRenderTests(UiTestFixture ui)
{
    [Fact]
    public Task LiveSelectionHasNoStartupInstructionsAndShowsSizeOnlyWhileDragging() => ui.Run(() =>
    {
        var monitor = new MonitorInfo("screen", new(0, 0, 64, 64), 1);
        var state = new CaptureEditorState();
        var surface = new EditorSurface(state, new DesktopLayout(monitor.Bounds, [monitor]), monitor) { IsLiveSelection = true };
        surface.Measure(new Size(64, 64)); surface.Arrange(new Rect(0, 0, 64, 64));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, Pixel(24, 30));
        state.Region = new(0, 0, 10, 10); state.CursorPosition = new(20, 20); state.IsDrawing = true;
        Assert.NotEqual(new byte[] { 255, 255, 255, 255 }, Pixel(10, 30));
        state.IsDrawing = false;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, Pixel(10, 30));

        byte[] Pixel(int x, int y)
        {
            using var output = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
            using (var context = output.CreateDrawingContext()) { context.FillRectangle(Brushes.White, new Rect(0, 0, 64, 64)); surface.Render(context); }
            var address = Marshal.AllocHGlobal(4);
            try { output.CopyPixels(new Avalonia.PixelRect(x, y, 1, 1), address, 4, 4); var sample = new byte[4]; Marshal.Copy(address, sample, 0, 4); return sample; }
            finally { Marshal.FreeHGlobal(address); }
        }
    });
    [Fact]
    public Task LiveSelectionDoesNotPaintSnapshotOrDimDesktop() => ui.Run(() =>
    {
        var pixels = new byte[64 * 64 * 4];
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i + 1] = 200; pixels[i + 3] = 255; }
        var monitor = new MonitorInfo("screen", new(0, 0, 64, 64), 1);
        var snapshot = new DesktopSnapshot(monitor.Bounds, [monitor], new(64, 64, 256, pixels));
        using var image = BitmapExportService.ToBitmap(snapshot.Frame);
        var state = new CaptureEditorState { Region = new(16, 16, 16, 16) };
        var surface = new EditorSurface(state, snapshot, monitor) { Image = image, IsLiveSelection = true };
        surface.Measure(new Size(64, 64)); surface.Arrange(new Rect(0, 0, 64, 64));
        using var output = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
        using (var context = output.CreateDrawingContext()) { context.FillRectangle(Brushes.White, new Rect(0, 0, 64, 64)); surface.Render(context); }
        var address = Marshal.AllocHGlobal(4);
        try
        {
            output.CopyPixels(new Avalonia.PixelRect(2, 60, 1, 1), address, 4, 4);
            var sample = new byte[4]; Marshal.Copy(address, sample, 0, 4);
            Assert.Equal(new byte[] { 255, 255, 255, 255 }, sample);
        }
        finally { Marshal.FreeHGlobal(address); }
    });
}
