namespace ShareVibeX.Core.Capture;

public sealed record DesktopLayout(PixelRect Bounds, IReadOnlyList<MonitorInfo> Monitors)
{
    public static DesktopLayout FromMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0 || monitors.Any(m => m.Bounds.IsEmpty || !double.IsFinite(m.Scale) || m.Scale <= 0))
            throw new InvalidOperationException(Loc.T("No valid desktop monitor layout is available."));
        var left = monitors.Min(m => m.Bounds.X); var top = monitors.Min(m => m.Bounds.Y);
        return new(new(left, top, checked(monitors.Max(m => m.Bounds.Right) - left), checked(monitors.Max(m => m.Bounds.Bottom) - top)), monitors.ToArray());
    }
}
