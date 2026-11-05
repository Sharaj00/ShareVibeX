using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ShareVibeX.Platform.Windows;

public static class WindowsFileClipboard
{
    public static byte[] EncodeFileDrop(string path)
    {
        if(!Path.IsPathFullyQualified(path)||path.Contains('\0'))throw new ArgumentException(Loc.T("An absolute file path is required."));
        var name=Encoding.Unicode.GetBytes(Path.GetFullPath(path)+"\0\0");var data=new byte[20+name.Length];
        BinaryPrimitives.WriteInt32LittleEndian(data,20);BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16),1);name.CopyTo(data,20);return data;
    }
    public static async Task PublishAsync(nint owner,string path,CancellationToken token=default)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(owner==0)throw new ArgumentException(Loc.T("A live clipboard owner window is required."));
        if(!File.Exists(path))throw new FileNotFoundException(Loc.T("The copied video is unavailable."),path);
        var effect=RegisterClipboardFormat("Preferred DropEffect");if(effect==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        using var files=new ClipboardMemory(EncodeFileDrop(path));using var copy=new ClipboardMemory([1,0,0,0]);
        token.ThrowIfCancellationRequested();
        for(var attempt=0;!OpenClipboard(owner);attempt++)
        {
            if(attempt==9)throw new Win32Exception(Marshal.GetLastWin32Error(),Loc.T("The clipboard is busy. Try again."));
            await Task.Delay(100,token);
        }
        try
        {
            token.ThrowIfCancellationRequested();if(!EmptyClipboard())throw new Win32Exception(Marshal.GetLastWin32Error());
            files.Publish(15);copy.Publish(effect);
        }
        finally{CloseClipboard();}
    }
    private sealed class ClipboardMemory : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ClipboardMemory(byte[] data) : base(true)
        {
            if (data.Length == 0) throw new ArgumentException(Loc.T("Clipboard image data is empty."), nameof(data));
            SetHandle(GlobalAlloc(0x0002, (nuint)data.Length)); // GMEM_MOVEABLE
            if (IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var address = GlobalLock(handle);
                if (address == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                try { Marshal.Copy(data, 0, address, data.Length); }
                finally { GlobalUnlock(handle); }
            }
            catch { Dispose(); throw; }
        }

        public void Publish(uint format)
        {
            if (SetClipboardData(format, handle) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            SetHandleAsInvalid(); // Successful SetClipboardData transfers ownership to Windows.
        }

        protected override bool ReleaseHandle() => GlobalFree(handle) == 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint owner);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint format, nint memory);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")]
    private static extern nint GlobalFree(nint memory);
}

