using ShareVibeX.App.Notifications;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Tests;

public sealed class CompletionPreviewTests
{
    [Fact]
    public void PreviewIsBoundedAndIndependentOfSourceLifetime()
    {
        var source=new CapturedFrame(1920,1080,1920*4,new byte[1920*1080*4]);source.Bgra32[2]=255;
        var preview=CompletionPreview.CreateFrame(source);
        Assert.Equal(480,preview.Width);Assert.Equal(270,preview.Height);Assert.Equal(480*270*4,preview.Bgra32.Length);
        Assert.Equal(255,preview.Bgra32[2]);source.Bgra32[2]=0;Assert.Equal(255,preview.Bgra32[2]);
    }
    [Fact]
    public void SmallScreenshotIsCopiedWithoutUpscalingOrDiskPath()
    {
        var frame=new CapturedFrame(2,1,8,new byte[]{1,2,3,255,4,5,6,255});
        var notice=new CompletionNotice("Screenshot copied",PreviewFrame:CompletionPreview.CreateFrame(frame));
        Assert.Null(notice.Path);Assert.Equal(2,notice.PreviewFrame!.Width);Assert.Equal(frame.Bgra32,notice.PreviewFrame.Bgra32);
    }
}
