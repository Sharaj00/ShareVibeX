using System.Diagnostics;
using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows;

public static class WindowsCaptureApplication
{
    public static string GetName()
    {
        if(!OperatingSystem.IsWindows())return "Desktop";
        return Name(GetForegroundWindow());
    }
    public static string GetName(PixelRect region)
    {
        if(!OperatingSystem.IsWindows())return "Desktop";
        var result="Desktop";var x=(long)region.X+region.Width/2;var y=(long)region.Y+region.Height/2;
        // Enumerate in Z order, ignoring our overlay windows. The region center identifies the captured application.
        EnumWindows((window,_)=>
        {
            GetWindowThreadProcessId(window,out var id);
            if(id==(uint)Environment.ProcessId||!IsWindowVisible(window)||IsIconic(window)||!GetWindowRect(window,out var bounds))return true;
            if(x<bounds.Left||x>=bounds.Right||y<bounds.Top||y>=bounds.Bottom)return true;
            if(DwmGetWindowAttribute(window,14,out var cloaked,sizeof(int))==0&&cloaked!=0)return true;
            result=Name(window);return false;
        },0);
        return result;
    }
    private static string Name(nint window)
    {
        try{GetWindowThreadProcessId(window,out var id);using var process=Process.GetProcessById((int)id);return process.ProcessName;}
        catch{return "Desktop";}
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect {public int Left,Top,Right,Bottom;}
    private delegate bool WindowCallback(nint window,nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback,nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out Rect bounds);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window,int attribute,out int value,int size);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint processId);
}
