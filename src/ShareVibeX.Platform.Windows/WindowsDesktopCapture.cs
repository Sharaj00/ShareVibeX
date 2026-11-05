using System.ComponentModel;
using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.Platform.Windows;

public sealed class WindowsDesktopCapture : IDesktopCapture
{
    public IReadOnlyList<MonitorInfo> GetMonitors() => WindowsMonitorProvider.GetMonitors();

    public Task<DesktopSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => Task.Run(() => Capture(cancellationToken), cancellationToken);

    public static void FlushDesktop()
    {
        if (OperatingSystem.IsWindows()) NativeMethods.DwmFlush();
    }

    private unsafe DesktopSnapshot Capture(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var monitors = GetMonitors();
        var left = monitors.Min(m => m.Bounds.X); var top = monitors.Min(m => m.Bounds.Y);
        var bounds = new PixelRect(left, top, monitors.Max(m => m.Bounds.Right)-left, monitors.Max(m => m.Bounds.Bottom)-top);
        var stride = checked(bounds.Width * 4); var length = checked(stride * bounds.Height);
        var screen = NativeMethods.GetDC(0);
        if (screen == 0) throw new Win32Exception();
        nint memory = 0, bitmap = 0, previous = 0;
        try
        {
            memory = NativeMethods.CreateCompatibleDC(screen);
            if (memory == 0) throw new Win32Exception();
            var info = new NativeMethods.BitmapInfo { Size = (uint)Marshal.SizeOf<NativeMethods.BitmapInfo>(), Width = bounds.Width,
                Height = -bounds.Height, Planes = 1, BitCount = 32, SizeImage = (uint)length };
            bitmap = NativeMethods.CreateDIBSection(screen, ref info, 0, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0) throw new Win32Exception();
            previous = NativeMethods.SelectObject(memory, bitmap);
            if (previous == 0 || previous == -1) throw new Win32Exception();
            new Span<byte>((void*)bits, length).Clear();
            foreach (var monitor in monitors)
            {
                token.ThrowIfCancellationRequested();
                var b = monitor.Bounds;
                if (!NativeMethods.BitBlt(memory, b.X-left, b.Y-top, b.Width, b.Height, screen, b.X, b.Y, 0x40CC0020))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), Loc.T("Desktop capture failed."));
            }
            var pixels = new byte[length];
            Marshal.Copy(bits, pixels, 0, length);
            for (var i = 3; i < length; i += 4) pixels[i] = 255;
            token.ThrowIfCancellationRequested();
            return new(bounds, monitors, new(bounds.Width, bounds.Height, stride, pixels));
        }
        finally
        {
            if (previous != 0 && previous != -1) NativeMethods.SelectObject(memory, previous);
            if (bitmap != 0) NativeMethods.DeleteObject(bitmap);
            if (memory != 0) NativeMethods.DeleteDC(memory);
            NativeMethods.ReleaseDC(0, screen);
        }
    }
}
