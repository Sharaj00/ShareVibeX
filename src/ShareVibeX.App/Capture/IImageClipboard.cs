using Avalonia.Media.Imaging;

namespace ShareVibeX.App.Capture;

public interface IImageClipboard
{
    Task PublishAsync(Bitmap bitmap);
    Task FlushAsync();
}
