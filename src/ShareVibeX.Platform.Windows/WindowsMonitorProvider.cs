using System.ComponentModel;
using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows;

public static class WindowsMonitorProvider
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
        =>GetTargets().Select(target=>target.Monitor).ToArray();

    public static Capture.WgcCaptureTarget GetCaptureTarget(MonitorInfo expected)
    {
        var target=GetTargets().FirstOrDefault(target=>target.Monitor.Id==expected.Id);
        if(target is null||target.Monitor!=expected)throw new InvalidOperationException(Loc.T("The display layout changed. Start a new capture."));
        return target;
    }
    private static IReadOnlyList<Capture.WgcCaptureTarget> GetTargets()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var monitors = new List<Capture.WgcCaptureTarget>();
        Exception? failure = null;
        bool Visit(nint monitor, nint dc, ref NativeMethods.Rect rect, nint data)
        {
            var info = new NativeMethods.MonitorInfoEx { Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>(), Device = "" };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) { failure = new Win32Exception(); return false; }
            var scale = NativeMethods.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96d : 1;
            monitors.Add(new(new(info.Device, new(info.Monitor.Left, info.Monitor.Top,
                info.Monitor.Right-info.Monitor.Left, info.Monitor.Bottom-info.Monitor.Top), scale),monitor));
            return true;
        }
        if (!NativeMethods.EnumDisplayMonitors(0, 0, Visit, 0) || failure is not null) throw failure ?? new Win32Exception();
        if (monitors.Count == 0) throw new InvalidOperationException(Loc.T("No displays are available."));
        return monitors.OrderBy(m => m.Monitor.Id, StringComparer.Ordinal).ToArray();
    }
}
