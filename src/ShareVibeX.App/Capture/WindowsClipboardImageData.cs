using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Settings;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Capture;

public sealed record WindowsClipboardImageData(byte[] Png, byte[] Dib)
{
    public static WindowsClipboardImageData Create(CapturedFrame frame) =>
        new(ImageEncoding.Encode(frame, ImageFormat.Png, 100), ClipboardDib.Encode(frame));
}
