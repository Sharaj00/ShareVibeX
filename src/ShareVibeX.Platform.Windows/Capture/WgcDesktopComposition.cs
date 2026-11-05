using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows.Capture;

public static class WgcDesktopComposition
{
    public static DesktopSnapshot Compose(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<WgcMonitorFrame> frames, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if(monitors.Count==0||frames.Count!=monitors.Count||monitors.Select(m=>m.Id).Distinct(StringComparer.Ordinal).Count()!=monitors.Count)
            throw new InvalidDataException("Invalid monitor frame set.");
        var ordered=monitors.OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray();
        foreach(var monitor in ordered)
        {
            if(monitor.Bounds.IsEmpty||!double.IsFinite(monitor.Scale)||monitor.Scale<=0)throw new InvalidDataException("Invalid monitor geometry.");
            var matches=frames.Where(f=>f.Monitor.Id==monitor.Id).ToArray();
            if(matches.Length!=1||matches[0].Monitor!=monitor)throw new InvalidDataException(Loc.T("The display layout changed. Start a new capture."));
            var frame=matches[0].Frame;var stride=checked(monitor.Bounds.Width*4);
            if(frame.Width!=monitor.Bounds.Width||frame.Height!=monitor.Bounds.Height||frame.Stride<stride||frame.Bgra32.Length<checked((frame.Height-1)*frame.Stride+stride))
                throw new InvalidDataException(Loc.T("The WGC frame does not match the capture monitor."));
        }
        var left=ordered.Min(m=>m.Bounds.X);var top=ordered.Min(m=>m.Bounds.Y);
        var bounds=new PixelRect(left,top,checked(ordered.Max(m=>m.Bounds.Right)-left),checked(ordered.Max(m=>m.Bounds.Bottom)-top));
        var outputStride=checked(bounds.Width*4);var pixels=new byte[checked(outputStride*bounds.Height)];
        for(var i=3;i<pixels.Length;i+=4)pixels[i]=255;
        foreach(var monitor in ordered)
        {
            var frame=frames.Single(f=>f.Monitor.Id==monitor.Id).Frame;var b=monitor.Bounds;
            for(var y=0;y<b.Height;y++)
            {
                token.ThrowIfCancellationRequested();
                var row=pixels.AsSpan(checked((b.Y-top+y)*outputStride+(b.X-left)*4),checked(b.Width*4));
                frame.Bgra32.AsSpan(checked(y*frame.Stride),row.Length).CopyTo(row);
                for(var x=3;x<row.Length;x+=4)row[x]=255;
            }
        }
        token.ThrowIfCancellationRequested();
        return new(bounds,ordered,new(bounds.Width,bounds.Height,outputStride,pixels));
    }
}
