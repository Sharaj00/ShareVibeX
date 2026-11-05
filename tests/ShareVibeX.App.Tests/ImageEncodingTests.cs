using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Settings;
using SkiaSharp;

namespace ShareVibeX.App.Tests;

public sealed class ImageEncodingTests
{
    private static CapturedFrame Fixture()
    {
        var random=new Random(27);var data=new byte[64*64*4];random.NextBytes(data);for(var i=3;i<data.Length;i+=4)data[i]=255;
        return new(64,64,256,data);
    }
    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Webp)]
    public void ChosenFormatDecodesToExpectedSize(ImageFormat format)
    {
        var encoded=ImageEncoding.Encode(Fixture(),format,90);
        using var codec=SKCodec.Create(new SKMemoryStream(encoded));
        Assert.Equal(64,codec.Info.Width);Assert.Equal(64,codec.Info.Height);
        Assert.Equal(format switch {ImageFormat.Png=>SKEncodedImageFormat.Png,ImageFormat.Jpeg=>SKEncodedImageFormat.Jpeg,_=>SKEncodedImageFormat.Webp},codec.EncodedFormat);
    }
    [Fact]
    public void JpegQualityChangesEncodedDataSize()
    {
        var frame=Fixture();Assert.True(ImageEncoding.Encode(frame,ImageFormat.Jpeg,95).Length>ImageEncoding.Encode(frame,ImageFormat.Jpeg,20).Length);
    }
    [Fact]
    public void PngPreservesExactColorsRegardlessOfQualitySetting()
    {
        var frame=new CapturedFrame(1,1,4,[10,20,30,255]);var encoded=ImageEncoding.Encode(frame,ImageFormat.Png,10);
        using var bitmap=SKBitmap.Decode(encoded);Assert.Equal(new SKColor(30,20,10,255),bitmap.GetPixel(0,0));
    }
}
