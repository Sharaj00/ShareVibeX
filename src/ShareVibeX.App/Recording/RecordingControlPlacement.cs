using Avalonia;
using ShareVibeX.Core.Capture;
using PixelRect=ShareVibeX.Core.Capture.PixelRect;
using PixelPoint=ShareVibeX.Core.Capture.PixelPoint;

namespace ShareVibeX.App.Recording;

public static class RecordingControlPlacement
{
    public static PixelPoint? TryPlace(PixelRect region,MonitorInfo monitor,Size logicalSize,bool excluded)
    {
        if(!double.IsFinite(monitor.Scale)||monitor.Scale<=0||!double.IsFinite(logicalSize.Width)||!double.IsFinite(logicalSize.Height)||logicalSize.Width<=0||logicalSize.Height<=0)return null;
        var sizeWidth=Math.Ceiling(logicalSize.Width*monitor.Scale);var sizeHeight=Math.Ceiling(logicalSize.Height*monitor.Scale);
        var bounds=monitor.Bounds;
        if(sizeWidth>bounds.Width||sizeHeight>bounds.Height)return null;
        var width=(int)sizeWidth;var height=(int)sizeHeight;var gap=(int)Math.Ceiling(10*monitor.Scale);
        var x=Math.Clamp(region.X+(region.Width-width)/2,bounds.X,bounds.Right-width);
        var y=Math.Clamp(region.Y,bounds.Y,bounds.Bottom-height);
        PixelPoint[] candidates=[new(x,Math.Clamp(region.Bottom+gap,bounds.Y,bounds.Bottom-height)),
            new(x,Math.Clamp(region.Y-height-gap,bounds.Y,bounds.Bottom-height)),
            new(Math.Clamp(region.X-width-gap,bounds.X,bounds.Right-width),y),
            new(Math.Clamp(region.Right+gap,bounds.X,bounds.Right-width),y)];
        foreach(var point in candidates)
            if(excluded || new PixelRect(point.X,point.Y,width,height).Intersect(region).IsEmpty)return point;
        return null;
    }
}
