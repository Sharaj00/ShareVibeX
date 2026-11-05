using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace ShareVibeX.App.Editing;

public static class AnnotationRenderer
{
    public static CapturedFrame Render(DesktopSnapshot snapshot,IReadOnlyList<Annotation> annotations)
    {
        var frame=snapshot.Frame;
        using var bitmap=new SKBitmap(new SKImageInfo(frame.Width,frame.Height,SKColorType.Bgra8888,SKAlphaType.Unpremul));
        for(var y=0;y<frame.Height;y++) Marshal.Copy(frame.Bgra32,y*frame.Stride,bitmap.GetPixels()+y*bitmap.RowBytes,frame.Width*4);
        using(var canvas=new SKCanvas(bitmap))
        foreach(var item in annotations)
        {
            var r=new SKRect(item.Bounds.X-snapshot.Bounds.X,item.Bounds.Y-snapshot.Bounds.Y,item.Bounds.Right-snapshot.Bounds.X,item.Bounds.Bottom-snapshot.Bounds.Y);
            using var paint=new SKPaint { Color=new SKColor(item.Color),IsAntialias=true,StrokeWidth=item.Width,Style=SKPaintStyle.Stroke,StrokeCap=SKStrokeCap.Round,StrokeJoin=SKStrokeJoin.Round };
            if(item.Kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse)
            {
                if(item.Effect==ShapeEffect.Blur)
                {
                    using var copy=bitmap.Copy();
                    using var filter=SKImageFilter.CreateBlur(Math.Max(1,item.Width*2),Math.Max(1,item.Width*2),SKShaderTileMode.Clamp);
                    using var blur=new SKPaint {ImageFilter=filter};
                    canvas.Save();
                    if(item.Kind==AnnotationKind.Ellipse){using var path=new SKPath();path.AddOval(r);canvas.ClipPath(path,antialias:false);}
                    else canvas.ClipRect(r);
                    canvas.DrawBitmap(copy,0,0,blur);canvas.Restore();
                }
                else
                {
                    paint.Style=item.Effect==ShapeEffect.Fill?SKPaintStyle.Fill:SKPaintStyle.Stroke;
                    if(item.Kind==AnnotationKind.Ellipse)canvas.DrawOval(r,paint);else canvas.DrawRect(r,paint);
                }
            }
            else if(item.Kind==AnnotationKind.Text)
            {
                if(item.Bounds.IsEmpty)continue;
                using var text=FittedTextLayout.Render(item);canvas.DrawBitmap(text,r.Left,r.Top);
            }
            else if(item.Points.Count>=2)
            {
                using var path=new SKPath();
                var points=item.Points.Select(p=>new SKPoint(p.X-snapshot.Bounds.X,p.Y-snapshot.Bounds.Y)).ToArray();
                path.MoveTo(points[0]);foreach(var point in points.Skip(1))path.LineTo(point);canvas.DrawPath(path,paint);
                if(item.Kind==AnnotationKind.Arrow)
                {
                    var end=points[^1];var start=points[0];var angle=Math.Atan2(end.Y-start.Y,end.X-start.X);var size=Math.Max(12,item.Width*4);
                    using var head=new SKPath();head.MoveTo(end);
                    head.LineTo(end.X-(float)Math.Cos(angle-0.5)*size,end.Y-(float)Math.Sin(angle-0.5)*size);
                    head.LineTo(end.X-(float)Math.Cos(angle+0.5)*size,end.Y-(float)Math.Sin(angle+0.5)*size);head.Close();paint.Style=SKPaintStyle.Fill;canvas.DrawPath(head,paint);
                }
            }
        }
        var pixels=new byte[checked(frame.Width*frame.Height*4)];
        for(var y=0;y<frame.Height;y++)Marshal.Copy(bitmap.GetPixels()+y*bitmap.RowBytes,pixels,y*frame.Width*4,frame.Width*4);
        return new(frame.Width,frame.Height,frame.Width*4,pixels);
    }
}
