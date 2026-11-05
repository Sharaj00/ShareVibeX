using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class ClipboardPublicationTests(UiTestFixture ui)
{
    [Fact]
    public Task CopyFlushesPixelsBeforeOwnerIsReleased()=>ui.Run(async()=>
    {
        var clipboard=new DeferredClipboard();var frame=new CapturedFrame(1,1,4,[10,20,30,255]);
        using(var owner=new ClipboardImageOwner())await owner.CopyAsync(clipboard,frame);
        Assert.Equal(new[]{"publish","flush"},clipboard.Calls);Assert.Equal(new byte[]{10,20,30,255},clipboard.Persisted);
    });
    [Fact]
    public Task FailedFlushRetainsPublishedBitmapForRetry()=>ui.Run(async()=>
    {
        var clipboard=new DeferredClipboard{FailFlush=true};using var owner=new ClipboardImageOwner();
        await Assert.ThrowsAsync<IOException>(()=>owner.CopyAsync(clipboard,new(1,1,4,[10,20,30,255])));
        clipboard.FailFlush=false;await clipboard.FlushAsync();Assert.Equal(new byte[]{10,20,30,255},clipboard.Persisted);
    });
    private sealed class DeferredClipboard:IImageClipboard
    {
        public List<string> Calls=[];public byte[]? Persisted;public bool FailFlush;private Bitmap? image;
        public Task PublishAsync(Bitmap bitmap){Calls.Add("publish");image=bitmap;return Task.CompletedTask;}
        public Task FlushAsync()
        {
            Calls.Add("flush");if(FailFlush)throw new IOException("Clipboard is busy.");
            var pointer=Marshal.AllocHGlobal(4);
            try{image!.CopyPixels(new Avalonia.PixelRect(0,0,1,1),pointer,4,4);Persisted=new byte[4];Marshal.Copy(pointer,Persisted,0,4);}
            finally{Marshal.FreeHGlobal(pointer);}
            return Task.CompletedTask;
        }
    }
}
