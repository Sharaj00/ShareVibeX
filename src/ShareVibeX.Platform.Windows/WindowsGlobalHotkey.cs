using System.ComponentModel;
using System.Runtime.InteropServices;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.Platform.Windows;

public sealed class WindowsGlobalHotkey : IGlobalHotkey
{
    private readonly string className = "ShareVibeX.Hotkey." + Guid.NewGuid().ToString("N");
    private readonly NativeMethods.WndProc procedure;
    private readonly nint instance;
    private nint handle;
    private int currentId;
    private int nextId;
    private HotkeyGesture? currentGesture;
    private Action? callback;

    public WindowsGlobalHotkey()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        procedure = WindowProcedure;
        instance = NativeMethods.GetModuleHandle(null);
        var cls = new NativeMethods.WindowClass { Procedure = procedure, Instance = instance, ClassName = className };
        if (NativeMethods.RegisterClass(ref cls) == 0) throw new Win32Exception();
        handle = NativeMethods.CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, -3, 0, instance, 0);
        if (handle == 0) { NativeMethods.UnregisterClass(className, instance); throw new Win32Exception(); }
    }

    public void Register(string gesture, Action onTriggered)
    {
        ObjectDisposedException.ThrowIf(handle == 0, this);
        var parsed = HotkeyGesture.Parse(gesture);
        if (currentGesture == parsed) { callback = onTriggered; return; }
        var id = ++nextId;
        if (!NativeMethods.RegisterHotKey(handle, id, parsed.Modifiers | 0x4000, parsed.VirtualKey))
            throw new Win32Exception(Marshal.GetLastWin32Error(), Loc.F("The shortcut {0} is unavailable. Choose another shortcut in Settings.",gesture));
        if (currentId != 0) NativeMethods.UnregisterHotKey(handle, currentId);
        currentId = id; currentGesture = parsed; callback = onTriggered;
    }

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        if (message == 0x0312 && (int)wParam == currentId) { callback?.Invoke(); return 0; }
        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Unregister()
    {
        if (currentId != 0) NativeMethods.UnregisterHotKey(handle, currentId);
        currentId = 0; callback = null; currentGesture = null;
    }

    public void Dispose()
    {
        if (handle == 0) return;
        Unregister(); NativeMethods.DestroyWindow(handle); handle = 0;
        NativeMethods.UnregisterClass(className, instance);
        GC.KeepAlive(procedure);
    }
}
