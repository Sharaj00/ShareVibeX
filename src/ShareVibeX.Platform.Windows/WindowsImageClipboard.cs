using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ShareVibeX.Platform.Windows;

public static class WindowsImageClipboard
{
    public static async Task PublishAsync(nint owner, byte[] png, byte[] dib)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (owner == 0) throw new ArgumentException(Loc.T("A live clipboard owner window is required."), nameof(owner));
        var pngFormat = RegisterClipboardFormat("PNG");
        var mimeFormat = RegisterClipboardFormat("image/png");
        if (pngFormat == 0 || mimeFormat == 0) throw new Win32Exception(Marshal.GetLastWin32Error());

        // Allocate and fill every format before replacing the existing clipboard contents.
        using var pngMemory = new ClipboardMemory(png);
        using var mimeMemory = new ClipboardMemory(png);
        using var dibMemory = new ClipboardMemory(dib);
        for (var attempt = 0; !OpenClipboard(owner); attempt++)
        {
            if (attempt == 9) throw new Win32Exception(Marshal.GetLastWin32Error(), Loc.T("The clipboard is busy. Try again."));
            await Task.Delay(100);
        }
        try
        {
            if (!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error());
            pngMemory.Publish(pngFormat);
            mimeMemory.Publish(mimeFormat);
            dibMemory.Publish(8); // CF_DIB; Windows synthesizes its color CF_BITMAP from this data.
        }
        finally { CloseClipboard(); }
        // All formats are eager system-owned copies. No delayed rendering or OLE flush is needed.
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
