using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ShareVibeX.App.Media;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class MediaThumbnailTests(UiTestFixture ui)
{
    [Fact]
    public Task TallImageThumbnailIsBoundedInBothDimensions()=>ui.Run(async()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"ShareVibeX-tall-"+Guid.NewGuid().ToString("N")+".png");
        try
        {
            using(var image=new WriteableBitmap(new(16,256),new(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque))image.Save(path,PngBitmapEncoderOptions.Default);
            using var thumbnail=await MediaThumbnailLoader.LoadAsync(path,width:64);
            Assert.NotNull(thumbnail);Assert.InRange(thumbnail.PixelSize.Width,1,64);Assert.InRange(thumbnail.PixelSize.Height,1,64);
        }
        finally{if(File.Exists(path))File.Delete(path);}
    });
}
