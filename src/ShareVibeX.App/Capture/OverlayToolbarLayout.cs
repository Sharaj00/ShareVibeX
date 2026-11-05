using Avalonia;

namespace ShareVibeX.App.Capture;

public static class OverlayToolbarLayout
{
    public static double EditorWidth(double physicalRegionWidth, double scale, double viewportWidth)
    {
        var available = Math.Max(1, viewportWidth - 24);
        return Math.Min(690, available);
    }
    public static Point Place(Rect region, Size toolbar, Size viewport)
    {
        const double margin = 12, gap = 10;
        var left = Math.Clamp(region.X, margin, Math.Max(margin, viewport.Width - toolbar.Width - margin));
        var top = region.Bottom + gap;
        if (top + toolbar.Height > viewport.Height - margin) top = region.Y - toolbar.Height - gap;
        return new(left, Math.Clamp(top, margin, Math.Max(margin, viewport.Height - toolbar.Height - margin)));
    }
}
