using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using ShareVibeX.Core.IO;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Settings;
using CaptureRect = ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Capture;

public sealed class BitmapExportService
{
    public Task SaveImageAsync(CapturedFrame frame,string destination,ImageFormat format,int quality,CancellationToken token=default)=>
        AtomicFile.WriteAsync(destination,async(stream,cancellation)=>
        {
            cancellation.ThrowIfCancellationRequested();var encoded=ImageEncoding.Encode(frame,format,quality);
            await stream.WriteAsync(encoded,cancellation);
        },token);
    public CapturedFrame Crop(DesktopSnapshot snapshot, CaptureRect desktopRegion)
    {
        if (desktopRegion.IsEmpty || desktopRegion.Intersect(snapshot.Bounds) != desktopRegion)
            throw new ArgumentOutOfRangeException(nameof(desktopRegion));
        var local = desktopRegion.ToFrameRect(snapshot.Bounds);
        var stride = checked(local.Width * 4);
        var pixels = new byte[checked(stride * local.Height)];
        for (var y=0; y<local.Height; y++)
            Buffer.BlockCopy(snapshot.Frame.Bgra32, checked((local.Y+y)*snapshot.Frame.Stride + local.X*4), pixels, y*stride, stride);
        return new(local.Width, local.Height, stride, pixels);
    }

    public Task SavePngAsync(CapturedFrame frame, string destination, CancellationToken cancellationToken = default) =>
        AtomicFile.WriteAsync(destination, (stream, token) =>
        {
            token.ThrowIfCancellationRequested();
            using var bitmap = ToBitmap(frame);
            bitmap.Save(stream,PngBitmapEncoderOptions.Default);
            return Task.CompletedTask;
        }, cancellationToken);

    public static WriteableBitmap ToBitmap(CapturedFrame frame)
    {
        var bitmap = new WriteableBitmap(new PixelSize(frame.Width,frame.Height),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Unpremul);
        using var target = bitmap.Lock();
        for (var y=0; y<frame.Height; y++)
            Marshal.Copy(frame.Bgra32,y*frame.Stride,target.Address+y*target.RowBytes,frame.Width*4);
        return bitmap;
    }
}
