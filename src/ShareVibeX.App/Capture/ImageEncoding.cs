using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Settings;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace ShareVibeX.App.Capture;

public static class ImageEncoding
{
    public static byte[] Encode(CapturedFrame frame,ImageFormat format,int quality)
    {
        if(quality is <1 or >100)throw new ArgumentOutOfRangeException(nameof(quality));
        using var bitmap=new SKBitmap(new SKImageInfo(frame.Width,frame.Height,SKColorType.Bgra8888,SKAlphaType.Unpremul));
        for(var y=0;y<frame.Height;y++)Marshal.Copy(frame.Bgra32,y*frame.Stride,bitmap.GetPixels()+y*bitmap.RowBytes,frame.Width*4);
        using var flattened=format==ImageFormat.Jpeg?new SKBitmap(new SKImageInfo(frame.Width,frame.Height,SKColorType.Bgra8888,SKAlphaType.Opaque)):null;
        if(flattened is not null){using var canvas=new SKCanvas(flattened);canvas.Clear(SKColors.White);canvas.DrawBitmap(bitmap,0,0);}
        using var image=SKImage.FromBitmap(flattened??bitmap);
        var encodedFormat=format switch{ImageFormat.Png=>SKEncodedImageFormat.Png,ImageFormat.Jpeg=>SKEncodedImageFormat.Jpeg,ImageFormat.Webp=>SKEncodedImageFormat.Webp,_=>throw new ArgumentOutOfRangeException(nameof(format))};
        using var encoded=image.Encode(encodedFormat,quality)??throw new IOException(Loc.T("The selected image encoder is unavailable."));
        return encoded.ToArray();
    }
    public static string Extension(ImageFormat format)=>format switch{ImageFormat.Png=>"png",ImageFormat.Jpeg=>"jpg",ImageFormat.Webp=>"webp",_=>throw new ArgumentOutOfRangeException(nameof(format))};
    public static ImageFormat FormatForPath(string path)=>Path.GetExtension(path).ToLowerInvariant() switch{".png"=>ImageFormat.Png,".jpg" or ".jpeg"=>ImageFormat.Jpeg,".webp"=>ImageFormat.Webp,_=>throw new InvalidDataException(Loc.T("Use a PNG, JPEG or WebP filename."))};
}
