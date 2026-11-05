using System.Buffers.Binary;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows;

public static class ClipboardDib
{
    public static byte[] Encode(CapturedFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Stride < checked(frame.Width * 4) ||
            frame.Bgra32.Length < checked(frame.Stride * frame.Height))
            throw new ArgumentException(Loc.T("The captured image dimensions or pixel buffer are invalid."), nameof(frame));

        var rowBytes = checked((checked(frame.Width * 3) + 3) & ~3);
        var imageSize = checked(rowBytes * frame.Height);
        var dib = new byte[checked(40 + imageSize)];
        BinaryPrimitives.WriteInt32LittleEndian(dib, 40);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), frame.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), 24);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(20), imageSize);
        // A positive height stores BGR rows bottom-up; each row is aligned to four bytes.
        // Screen captures are opaque. Omitting alpha also avoids legacy DIB alpha interpretation.
        for (var y = 0; y < frame.Height; y++)
        {
            var source = y * frame.Stride;
            var target = 40 + (frame.Height - 1 - y) * rowBytes;
            for (var x = 0; x < frame.Width; x++)
            {
                dib[target++] = frame.Bgra32[source++];
                dib[target++] = frame.Bgra32[source++];
                dib[target++] = frame.Bgra32[source++];
                source++;
            }
        }
        return dib;
    }
}
