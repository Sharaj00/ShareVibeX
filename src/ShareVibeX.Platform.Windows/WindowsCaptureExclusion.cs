using System.Runtime.InteropServices;

namespace ShareVibeX.Platform.Windows;

public static class WindowsCaptureExclusion
{
    public static bool TryExclude(nint window)=>OperatingSystem.IsWindows() && window!=0 && SetWindowDisplayAffinity(window,0x11);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint window,uint affinity);
}
