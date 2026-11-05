using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Capture;

public sealed class ClipboardImageOwner : IDisposable
{
    private Bitmap? retained;

    public async Task CopyAsync(IImageClipboard clipboard,CapturedFrame frame)
    {
        var next=BitmapExportService.ToBitmap(frame);
        try{await clipboard.PublishAsync(next);}catch{next.Dispose();throw;}
        retained?.Dispose();retained=next;
        await clipboard.FlushAsync();
    }

    public async Task CopyAsync(TopLevel owner, CapturedFrame frame)
    {
        if (OperatingSystem.IsWindows())
        {
            var handle = owner.TryGetPlatformHandle()?.Handle ?? 0;
            var data = WindowsClipboardImageData.Create(frame);
            await WindowsImageClipboard.PublishAsync(handle, data.Png, data.Dib);
            retained?.Dispose();
            retained = null;
            return;
        }
        var clipboard = owner.Clipboard ?? throw new InvalidOperationException(Loc.T("The clipboard is unavailable. Try again."));
        await CopyAsync(new PlatformClipboard(clipboard),frame);
    }

    public void Dispose() { retained?.Dispose(); retained = null; }

    private sealed class PlatformClipboard(IClipboard clipboard):IImageClipboard
    {
        public Task PublishAsync(Bitmap bitmap)=>clipboard.SetBitmapAsync(bitmap);
        public Task FlushAsync()=>clipboard.FlushAsync();
    }
}
