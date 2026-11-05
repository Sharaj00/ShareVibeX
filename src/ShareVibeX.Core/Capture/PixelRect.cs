namespace ShareVibeX.Core.Capture;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public static PixelRect FromPoints(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), checked(Math.Abs(b.X - a.X)), checked(Math.Abs(b.Y - a.Y)));

    public PixelRect Intersect(PixelRect other)
    {
        var x = Math.Max(X, other.X); var y = Math.Max(Y, other.Y);
        return new(x, y, Math.Max(0, Math.Min(Right, other.Right) - x), Math.Max(0, Math.Min(Bottom, other.Bottom) - y));
    }

    public PixelRect ToFrameRect(PixelRect desktopBounds) => new(checked(X - desktopBounds.X), checked(Y - desktopBounds.Y), Width, Height);
}
