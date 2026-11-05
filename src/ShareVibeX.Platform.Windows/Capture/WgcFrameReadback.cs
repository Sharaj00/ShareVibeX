using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows.Capture;

public static class WgcFrameReadback
{
    public static CapturedFrame CopyBgraRows(ReadOnlySpan<byte> source, int width, int height, int rowPitch, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int stride = checked(width * 4);
        if (rowPitch < stride) throw new ArgumentOutOfRangeException(nameof(rowPitch));
        int length = checked(stride * height);
        int required = checked((height - 1) * rowPitch + stride);
        if (source.Length < required) throw new ArgumentException("The source buffer does not contain all frame rows.", nameof(source));
        token.ThrowIfCancellationRequested();
        var pixels = new byte[length];
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            var row = pixels.AsSpan(y * stride, stride);
            source.Slice(y * rowPitch, stride).CopyTo(row);
            for (int x = 3; x < stride; x += 4) row[x] = 255;
        }
        return new CapturedFrame(width, height, stride, pixels);
    }
}
