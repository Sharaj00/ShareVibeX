using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows;

public static class WindowsSelectionTools
{
    private static readonly object CursorGate=new();
    private static readonly List<CursorLease> CursorLeases=[];
    public static PixelPoint GetCursorPosition()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(!GetCursorPos(out var point))throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(point.X,point.Y);
    }

    public static PixelRect? FindRegion(PixelPoint point,bool includeControls)
    {
        if(!OperatingSystem.IsWindows())return null;
        PixelRect? result=null;var desktop=GetDesktopWindow();var shell=GetShellWindow();
        EnumWindows((window,_)=>
        {
            try
            {
                GetWindowThreadProcessId(window,out var process);
                if(process==(uint)Environment.ProcessId||window==desktop||window==shell||!IsWindowVisible(window)||IsIconic(window))return true;
                var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);
                if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")return true;
                if(DwmGetWindowAttribute(window,14,out var cloaked,sizeof(int))==0&&cloaked!=0)return true;
                if(GetWindowDisplayAffinity(window,out var affinity)&&affinity!=0)return true;
                if(!GetWindowRect(window,out var native))return true;
                // DWM frame bounds exclude invisible resize margins when available.
                if(DwmGetFrameBounds(window,9,out var frame,Marshal.SizeOf<Rect>())==0)native=frame;
                if(!TryRegion(native,out var bounds)||!Contains(bounds,point))return true;
                result=includeControls?FindControl(window,point,bounds):bounds;
                return false;
            }
            catch{return true;}
        },0);
        return result;
    }

    private static PixelRect FindControl(nint window,PixelPoint point,PixelRect topBounds)
    {
        var best=topBounds;var current=window;
        for(var depth=0;depth<32;depth++)
        {
            var client=new Point{X=point.X,Y=point.Y};if(!ScreenToClient(current,ref client))break;
            var child=ChildWindowFromPointEx(current,client,1|2|4);
            if(child==0||child==current||!IsWindowVisible(child))break;
            GetWindowThreadProcessId(child,out var process);if(process==(uint)Environment.ProcessId)break;
            if(!GetWindowRect(child,out var native)||!TryRegion(native,out var bounds))break;
            bounds=bounds.Intersect(topBounds);if(bounds.IsEmpty||!Contains(bounds,point))break;
            best=bounds;current=child;
        }
        return best;
    }

    public static IDisposable ConstrainCursor(PixelRect bounds)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(bounds.IsEmpty)throw new ArgumentOutOfRangeException(nameof(bounds));
        var requested=new Rect{Left=bounds.X,Top=bounds.Y,Right=bounds.Right,Bottom=bounds.Bottom};
        lock(CursorGate)
        {
        if(!GetClipCursor(out var previous))throw new Win32Exception(Marshal.GetLastWin32Error());
        if(!ClipCursor(ref requested))throw new Win32Exception(Marshal.GetLastWin32Error());
        if(GetClipCursor(out var applied)){var lease=new CursorLease(previous,applied);CursorLeases.Add(lease);return lease;}
        var error=Marshal.GetLastWin32Error();ClipCursor(ref previous);throw new Win32Exception(error);
        }
    }
    public static void ReleaseCursorConstraints()
    {
        lock(CursorGate)foreach(var lease in CursorLeases.AsEnumerable().Reverse().ToArray())lease.Dispose();
    }

    private sealed class CursorLease(Rect previous,Rect applied):IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            lock(CursorGate)
            {
                if(Interlocked.Exchange(ref disposed,1)!=0)return;
                CursorLeases.Remove(this);
                // Do not replace a later cursor constraint installed by another owner.
                if(GetClipCursor(out var current)&&current.Equals(applied))ClipCursor(ref previous);
            }
        }
    }
    public static bool Contains(PixelRect bounds,PixelPoint point)=>!bounds.IsEmpty&&point.X>=bounds.X&&point.Y>=bounds.Y&&(long)point.X<(long)bounds.X+bounds.Width&&(long)point.Y<(long)bounds.Y+bounds.Height;
    private static bool TryRegion(Rect native,out PixelRect bounds)
    {
        var width=(long)native.Right-native.Left;var height=(long)native.Bottom-native.Top;
        if(width<=0||height<=0||width>int.MaxValue||height>int.MaxValue){bounds=default;return false;}
        bounds=new(native.Left,native.Top,(int)width,(int)height);return true;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct Rect:IEquatable<Rect>
    {
        public int Left,Top,Right,Bottom;
        public readonly bool Equals(Rect other)=>Left==other.Left&&Top==other.Top&&Right==other.Right&&Bottom==other.Bottom;
    }
    private delegate bool WindowCallback(nint window,nint parameter);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback,nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(nint window,StringBuilder name,int size);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(nint window,out uint affinity);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out Rect bounds);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint window,ref Point point);
    [DllImport("user32.dll")] private static extern nint ChildWindowFromPointEx(nint window,Point point,uint flags);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetClipCursor(out Rect bounds);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool ClipCursor(ref Rect bounds);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window,int attribute,out int value,int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] private static extern int DwmGetFrameBounds(nint window,int attribute,out Rect bounds,int size);
}
