using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ShareVibeX.Platform.Windows;

public sealed record WindowsTrayMenuItem(string Text,bool Enabled,Action Invoke);

public sealed class WindowsTrayIcon:IDisposable
{
    private const uint Callback=0x8001;
    private readonly string className="ShareVibeX.Tray."+Guid.NewGuid().ToString("N");
    private readonly NativeMethods.WndProc procedure;private readonly nint instance;private nint handle,icon;
    private readonly uint taskbarCreated;private NotifyData data;
    public event Action<uint>? Input;
    public event Action<Exception>? Error;
    public Func<IReadOnlyList<WindowsTrayMenuItem>>? Menu {get;set;}
    public static int DoubleClickMilliseconds=>(int)GetDoubleClickTime();
    public WindowsTrayIcon(byte[] ico)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        procedure=WindowProcedure;instance=NativeMethods.GetModuleHandle(null);taskbarCreated=RegisterWindowMessage("TaskbarCreated");
        var cls=new NativeMethods.WindowClass{Procedure=procedure,Instance=instance,ClassName=className,Style=8};
        if(NativeMethods.RegisterClass(ref cls)==0)throw new Win32Exception();
        try
        {
            // A hidden top-level window receives Explorer's TaskbarCreated broadcast.
            handle=NativeMethods.CreateWindowEx(0,className,"",0,0,0,0,0,0,0,instance,0);
            if(handle==0)throw new Win32Exception();
            var image=ReadIconImage(ico);icon=CreateIconFromResourceEx(image,(uint)image.Length,true,0x30000,32,32,0);
            // ICO containers are loaded from memory through their image directory entry.
            if(icon==0)throw new Win32Exception(Loc.T("Cannot load notification icon."));
            data=new(){Size=(uint)Marshal.SizeOf<NotifyData>(),Window=handle,Id=1,Flags=1|2|4|128,Callback=Callback,Icon=icon,Tip="ShareVibeX",Info="",InfoTitle=""};
            Add();
        }
        catch{Dispose();throw;}
    }
    public static byte[] ReadIconImage(byte[] ico)
    {
        if(ico.Length<6||BitConverter.ToUInt16(ico,0)!=0||BitConverter.ToUInt16(ico,2)!=1)throw new InvalidDataException(Loc.T("Invalid ICO header."));
        var count=BitConverter.ToUInt16(ico,4);if(count==0||6+16*count>ico.Length)throw new InvalidDataException(Loc.T("Invalid ICO directory."));
        var index=Enumerable.Range(0,count).OrderBy(i=>Math.Abs((ico[6+16*i]==0?256:ico[6+16*i])-32)).First();
        var size=BitConverter.ToUInt32(ico,6+16*index+8);var offset=BitConverter.ToUInt32(ico,6+16*index+12);
        if(size==0||offset<6+16*count||(long)offset+size>ico.Length)throw new InvalidDataException(Loc.T("Invalid ICO image."));
        return ico.AsSpan((int)offset,(int)size).ToArray();
    }
    private void Add()
    {
        if(!Shell_NotifyIcon(0,ref data))throw new Win32Exception(Loc.T("Cannot create notification icon."));
        data.Version=4;if(!Shell_NotifyIcon(4,ref data))throw new Win32Exception(Loc.T("Cannot configure notification icon."));
    }
    public void SetToolTip(string text)
    {
        if(handle==0)return;data.Tip=text[..Math.Min(127,text.Length)];Shell_NotifyIcon(1,ref data);
    }
    private nint WindowProcedure(nint hwnd,uint message,nuint wParam,nint lParam)
    {
        try
        {
            if(message==taskbarCreated&&handle!=0){Add();return 0;}
            if(message==Callback)
            {
                var input=(uint)((long)lParam&0xffff);
                if(input==0x7b){Input?.Invoke(input);ShowMenu();}
                else Input?.Invoke(input);
                return 0;
            }
        }
        catch(Exception error){Error?.Invoke(error);}
        return NativeMethods.DefWindowProc(hwnd,message,wParam,lParam);
    }
    private void ShowMenu()
    {
        var entries=Menu?.Invoke();if(entries is null)return;
        var menu=CreatePopupMenu();if(menu==0)throw new Win32Exception();
        uint selected;
        try
        {
            for(var i=0;i<entries.Count;i++)if(!AppendMenu(menu,entries[i].Enabled?0u:1u,(nuint)(i+1),entries[i].Text))throw new Win32Exception();
            GetCursorPos(out var point);SetForegroundWindow(handle);
            selected=TrackPopupMenuEx(menu,0x100|0x2,point.X,point.Y,handle,0);PostMessage(handle,0,0,0);
        }
        finally{DestroyMenu(menu);}
        if(selected>0&&selected<=entries.Count)entries[(int)selected-1].Invoke();
    }
    public void Dispose()
    {
        if(handle!=0){Shell_NotifyIcon(2,ref data);NativeMethods.DestroyWindow(handle);handle=0;}
        if(icon!=0){DestroyIcon(icon);icon=0;}
        NativeMethods.UnregisterClass(className,instance);GC.KeepAlive(procedure);
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct NotifyData
    {
        public uint Size;public nint Window;public uint Id,Flags,Callback;public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
        public uint State,StateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string InfoTitle;
        public uint InfoFlags;public Guid Guid;public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point {public int X,Y;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="Shell_NotifyIconW")] private static extern bool Shell_NotifyIcon(uint message,ref NotifyData data);
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] private static extern nint CreateIconFromResourceEx(byte[] bytes,uint size,bool icon,uint version,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool AppendMenu(nint menu,uint flags,nuint id,string text);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu,uint flags,int x,int y,nint owner,nint options);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window,uint message,nuint wParam,nint lParam);
}
