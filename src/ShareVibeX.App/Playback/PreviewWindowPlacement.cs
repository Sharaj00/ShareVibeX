using Avalonia;
using ShareVibeX.Core.Capture;
using R=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Playback;

public sealed record PreviewPlacement(Avalonia.PixelPoint Position,Size Size,MonitorInfo Monitor);
public static class PreviewWindowPlacement
{
    public static PreviewPlacement ForRegion(R region,IReadOnlyList<MonitorInfo> monitors)
    {
        if(region.IsEmpty)throw new ArgumentException(Loc.T("A nonempty preview region is required."));
        var monitor=monitors.FirstOrDefault(m=>region.X>=m.Bounds.X&&region.X<m.Bounds.Right&&region.Y>=m.Bounds.Y&&region.Y<m.Bounds.Bottom)
            ??monitors.OrderByDescending(m=>(long)m.Bounds.Intersect(region).Width*m.Bounds.Intersect(region).Height).FirstOrDefault()
            ??throw new InvalidOperationException(Loc.T("No preview monitor is available."));
        if(!double.IsFinite(monitor.Scale)||monitor.Scale<=0)throw new InvalidDataException(Loc.T("The monitor scale is invalid."));
        return new(new(region.X,region.Y),new(region.Width/monitor.Scale,region.Height/monitor.Scale),monitor);
    }
}
