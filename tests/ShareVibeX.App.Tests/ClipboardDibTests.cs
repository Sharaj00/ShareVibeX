using System.Buffers.Binary;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;
using ShareVibeX.Platform.Windows;
using SkiaSharp;

namespace ShareVibeX.App.Tests;

public sealed class ClipboardDibTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ColorDibPreservesRowsAndChannelsWithPaddedSource(int width)
    {
        var stride = width * 4 + 8;
        var pixels = Enumerable.Repeat((byte)0xED, stride * 2).ToArray();
        for (var y = 0; y < 2; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = y * stride + x * 4;
            pixels[offset] = (byte)(17 + x * 25);
            pixels[offset + 1] = (byte)(41 + y * 91);
            pixels[offset + 2] = (byte)(207 - x * 30);
            pixels[offset + 3] = 255;
        }
        var payload = WindowsClipboardImageData.Create(new CapturedFrame(width, 2, stride, pixels));
        var dib = payload.Dib;
        using var png = SKBitmap.Decode(payload.Png);
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(dib));
        Assert.Equal(width, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8)));
        Assert.Equal(24, BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14)));
        var rowBytes = (width * 3 + 3) & ~3;
        Assert.Equal(40 + rowBytes * 2, dib.Length);
        for (var y = 0; y < 2; y++)
            Assert.All(dib.AsSpan(40 + y * rowBytes + width * 3, rowBytes - width * 3).ToArray(), b => Assert.Equal(0, b));

        // Decode through an independent BMP decoder, rather than duplicating DIB row conversion.
        var bmp = new byte[14 + dib.Length];
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), bmp.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), 54);
        dib.CopyTo(bmp, 14);
        using var decoded = SKBitmap.Decode(bmp);
        Assert.NotNull(decoded);
        for (var y = 0; y < 2; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = y * stride + x * 4;
            Assert.Equal(new SKColor(pixels[offset + 2], pixels[offset + 1], pixels[offset]), decoded.GetPixel(x, y));
            Assert.Equal(png.GetPixel(x, y), decoded.GetPixel(x, y));
        }
    }

    [Fact]
    public async Task ClipboardPngAndDibMatchSavedEditedCrop()
    {
        var pixels = new byte[9 * 5 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 32; pixels[i + 1] = 43; pixels[i + 2] = 65; pixels[i + 3] = 255;
        }
        var snapshot = new DesktopSnapshot(new(-9, -5, 9, 5), [], new(9, 5, 36, pixels));
        var edited = AnnotationRenderer.Render(snapshot,
            [new(Guid.NewGuid(), AnnotationKind.Rectangle, new(-7, -4, 3, 2), [], 0xFF19D9F1, Effect: ShapeEffect.Fill)]);
        var export = new BitmapExportService();
        var crop = export.Crop(snapshot with { Frame = edited }, new(-8, -5, 7, 5));
        var payload = WindowsClipboardImageData.Create(crop);
        var path = Path.Combine(Path.GetTempPath(), $"sharevibex-clipboard-{Guid.NewGuid():N}.png");
        try
        {
            await export.SaveImageAsync(crop, path, ShareVibeX.Core.Settings.ImageFormat.Png, 100);
            Assert.Equal(await File.ReadAllBytesAsync(path), payload.Png);
            var bmp = new byte[14 + payload.Dib.Length];
            bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), bmp.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), 54);
            payload.Dib.CopyTo(bmp, 14);
            using var dib = SKBitmap.Decode(bmp);
            using var png = SKBitmap.Decode(payload.Png);
            Assert.NotNull(dib);
            for (var y = 0; y < png.Height; y++)
            for (var x = 0; x < png.Width; x++)
                Assert.Equal(png.GetPixel(x, y), dib.GetPixel(x, y));
            Assert.Equal(new SKColor(0x19, 0xD9, 0xF1), dib.GetPixel(2, 2));
            Assert.Equal(new SKColor(65, 43, 32), dib.GetPixel(6, 4));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
