using ShareVibeX.Core.Capture;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using ShareVibeX.Core.Editing;
using SkiaSharp;
using R=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Editing;

public static class FittedTextLayout
{
    public const int Padding=4;
    public static string[] Lines(string text)=>text.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
    public static float FontSize(string text,R bounds)
    {
        var width=Math.Max(1,bounds.Width-Padding*2);var height=Math.Max(1,bounds.Height-Padding*2);
        var low=1d;var high=Math.Clamp(height/(Math.Max(1,Lines(text).Length)*1.3),1,512);
        for(var iteration=0;iteration<12;iteration++)
        {
            var size=(low+high)/2;using var layout=Create(text,size,Brushes.White);
            if(layout.WidthIncludingTrailingWhitespace<=width && layout.Height<=height)low=size;else high=size;
        }
        return (float)low;
    }
    private static TextLayout Create(string text,double size,IBrush color)=>new(string.Join('\n',Lines(text)),new Typeface("Segoe UI, Inter, sans-serif"),size,color,
        textWrapping:TextWrapping.NoWrap,lineHeight:size*1.3);
    public static SKBitmap Render(Annotation annotation)
    {
        using var target=new RenderTargetBitmap(new PixelSize(annotation.Bounds.Width,annotation.Bounds.Height),new Vector(96,96));
        using(var context=target.CreateDrawingContext())
        using(var layout=Create(annotation.Text,FontSize(annotation.Text,annotation.Bounds),new SolidColorBrush(Color.FromUInt32(annotation.Color))))
            layout.Draw(context,new Point(Padding,Padding));
        var format=target.Format==Avalonia.Platform.PixelFormat.Rgba8888?SKColorType.Rgba8888:SKColorType.Bgra8888;
        var bitmap=new SKBitmap(new SKImageInfo(annotation.Bounds.Width,annotation.Bounds.Height,format,SKAlphaType.Premul));
        try{target.CopyPixels(new Avalonia.PixelRect(0,0,annotation.Bounds.Width,annotation.Bounds.Height),bitmap.GetPixels(),bitmap.ByteCount,bitmap.RowBytes);return bitmap;}
        catch{bitmap.Dispose();throw;}
    }
}
