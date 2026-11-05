using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ShareVibeX.Platform.Windows;

/// <summary>Windows Shell operations for a single user-selected history file.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsHistoryFileActions
{
    private static readonly Guid ShellItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid ContextMenuId = new("000214E4-0000-0000-C000-000000000046");
    private static readonly Guid ShellUiHandler = new("3981E225-F559-11D3-8E3A-00C04F6837D5");
    private static long nextSubclass;

    public static void ShowContextMenu(string path, nint owner, int x, int y)
    {
        if (owner == 0) throw new InvalidOperationException("A window is required for the file menu.");
        ValidateFile(path);
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        IShellItem? item = null; object? context = null; nint menu = 0;
        var subclass = (nuint)Interlocked.Increment(ref nextSubclass);
        SubclassProcedure procedure = (hwnd, message, wParam, lParam, _, _) =>
        {
            // Extensions require menu initialization, owner-draw and keyboard messages.
            try
            {
                if (message is 0x117 or 0x2B or 0x2C or 0x120)
                {
                    if (context is IContextMenu3 third && third.HandleMenuMsg2(message, wParam, lParam, out var result) == 0) return result;
                    if (context is IContextMenu2 second && second.HandleMenuMsg(message, wParam, lParam) == 0) return message is 0x2B or 0x2C ? 1 : 0;
                }
            }
            catch { /* An extension cannot unwind through the native window procedure. */ }
            return DefSubclassProc(hwnd, message, wParam, lParam);
        };
        var installed = false;
        try
        {
            item = CreateItem(path);
            var handler = ShellUiHandler; var iid = ContextMenuId;
            item.BindToHandler(0, ref handler, ref iid, out context);
            var shellMenu = (IContextMenu)context;
            if (!SetWindowSubclass(owner, procedure, subclass, 0)) throw new Win32Exception();
            installed = true;
            menu = CreatePopupMenu(); if (menu == 0) throw new Win32Exception();
            Marshal.ThrowExceptionForHR(shellMenu.QueryContextMenu(menu, 0, 1, 0x7FFF, 0));
            SetForegroundWindow(owner);
            var command = TrackPopupMenuEx(menu, 0x100 | 0x2, x, y, owner, 0);
            // The hook is required only while the menu is tracking, not during a Shell command.
            RemoveWindowSubclass(owner, procedure, subclass); installed = false;
            if (command != 0)
            {
                var invocation = new InvokeInfo
                {
                    Size = Marshal.SizeOf<InvokeInfo>(), Mask = 0x4000 | 0x20000000,
                    Window = owner, Verb = (nint)(command - 1), VerbUnicode = (nint)(command - 1),
                    Show = 1, Point = new() { X = x, Y = y }
                };
                shellMenu.InvokeCommand(ref invocation);
            }
        }
        finally
        {
            if (installed) RemoveWindowSubclass(owner, procedure, subclass);
            if (menu != 0) DestroyMenu(menu);
            if (context is not null) Marshal.ReleaseComObject(context);
            if (item is not null) Marshal.ReleaseComObject(item);
            GC.KeepAlive(procedure); OleUninitialize();
        }
    }

    public static Task<bool> RecycleAsync(string path, nint owner)
    {
        ValidateFile(path);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(Recycle(path, owner)); }
            catch (Exception error) { completion.TrySetException(error); }
        }) { IsBackground = true, Name = "ShareVibeX recycle file" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    private static bool Recycle(string path, nint owner)
    {
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        IFileOperation? operation = null; IShellItem? item = null;
        try
        {
            operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"), true)!)!;
            // Recycle only, no connected items, no error/progress UI, stop at the first failure.
            // The sink vetoes any Shell attempt to fall back to a permanent deletion.
            operation.SetOperationFlags(0x80000 | 0x20000000 | 0x100000 | 0x400 | 0x2000 | 0x4 | 0x10 | 0x4000);
            operation.SetOwnerWindow(owner); item = CreateItem(path); var sink = new RecycleSink();
            operation.DeleteItem(item, sink);
            try { operation.PerformOperations(); }
            catch (COMException) when (sink.PermanentDeleteRejected) { throw new IOException("The Recycle Bin is unavailable for this file. The file was not deleted."); }
            catch (COMException error) when (error.HResult == unchecked((int)0x800704C7)) { return false; }
            operation.GetAnyOperationsAborted(out var aborted);
            if (sink.PermanentDeleteRejected) throw new IOException("The Recycle Bin is unavailable for this file. The file was not deleted.");
            if (sink.Error < 0 && sink.Error != unchecked((int)0x800704C7)) Marshal.ThrowExceptionForHR(sink.Error);
            return !aborted && sink.Recycled;
        }
        finally
        {
            if (item is not null) Marshal.ReleaseComObject(item);
            if (operation is not null) Marshal.ReleaseComObject(operation);
            OleUninitialize();
        }
    }

    private static void ValidateFile(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("A full file path is required.", nameof(path));
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new IOException("Only regular media files can be used here.");
    }
    private static IShellItem CreateItem(string path)
    { var iid = ShellItemId; Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, 0, ref iid, out var item)); return item; }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct InvokeInfo
    {
        public int Size; public uint Mask; public nint Window, Verb, Parameters, Directory; public int Show;
        public uint HotKey; public nint Icon, Title, VerbUnicode, ParametersUnicode, DirectoryUnicode, TitleUnicode; public Point Point;
    }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint context, ref Guid handler, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
        void GetParent(out IShellItem parent); void GetDisplayName(uint kind, out nint name);
        void GetAttributes(uint mask, out uint attributes); void Compare(IShellItem other, uint hint, out int order);
    }
    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint position, uint first, uint last, uint flags);
        void InvokeCommand(ref InvokeInfo info); void GetCommandString(nuint id, uint flags, nint reserved, nint name, uint count);
    }
    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint position, uint first, uint last, uint flags);
        void InvokeCommand(ref InvokeInfo info); void GetCommandString(nuint id, uint flags, nint reserved, nint name, uint count);
        [PreserveSig] int HandleMenuMsg(uint message, nuint wParam, nint lParam);
    }
    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint position, uint first, uint last, uint flags);
        void InvokeCommand(ref InvokeInfo info); void GetCommandString(nuint id, uint flags, nint reserved, nint name, uint count);
        [PreserveSig] int HandleMenuMsg(uint message, nuint wParam, nint lParam);
        [PreserveSig] int HandleMenuMsg2(uint message, nuint wParam, nint lParam, out nint result);
    }
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(nint sink, out uint cookie); void Unadvise(uint cookie); void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message); void SetProgressDialog(nint dialog);
        void SetProperties(nint properties); void SetOwnerWindow(nint window);
        void ApplyPropertiesToItem(IShellItem item); void ApplyPropertiesToItems(nint items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, nint sink); void RenameItems(nint items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, nint sink); void MoveItems(nint items, IShellItem destination);
        void CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, nint sink); void CopyItems(nint items, IShellItem destination);
        void DeleteItem(IShellItem item, IFileOperationProgressSink sink); void DeleteItems(nint items);
        void NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, nint sink);
        void PerformOperations(); void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
    [ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileOperationProgressSink
    {
        [PreserveSig] int StartOperations(); [PreserveSig] int FinishOperations(int result);
        [PreserveSig] int PreRenameItem(uint flags, nint item, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostRenameItem(uint flags, nint item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, nint created);
        [PreserveSig] int PreMoveItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostMoveItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, nint created);
        [PreserveSig] int PreCopyItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostCopyItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, nint created);
        [PreserveSig] int PreDeleteItem(uint flags, nint item);
        [PreserveSig] int PostDeleteItem(uint flags, nint item, int result, nint created);
        [PreserveSig] int PreNewItem(uint flags, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostNewItem(uint flags, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, nint created);
        [PreserveSig] int UpdateProgress(uint total, uint complete); [PreserveSig] int ResetTimer(); [PreserveSig] int PauseTimer(); [PreserveSig] int ResumeTimer();
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class RecycleSink : IFileOperationProgressSink
    {
        public bool PermanentDeleteRejected { get; private set; }
        public bool Recycled { get; private set; }
        public int Error { get; private set; }
        public int PreDeleteItem(uint flags, nint item)
        { if ((flags & 0x80) != 0) return 0; PermanentDeleteRejected = true; return unchecked((int)0x80004004); }
        public int PostDeleteItem(uint flags, nint item, int result, nint created)
        { Error = result; Recycled = result == 0 && created != 0; return 0; }
        public int StartOperations() => 0; public int FinishOperations(int result) => 0;
        public int PreRenameItem(uint flags, nint item, string name) => 0;
        public int PostRenameItem(uint flags, nint item, string name, int result, nint created) => 0;
        public int PreMoveItem(uint flags, nint item, nint destination, string name) => 0;
        public int PostMoveItem(uint flags, nint item, nint destination, string name, int result, nint created) => 0;
        public int PreCopyItem(uint flags, nint item, nint destination, string name) => 0;
        public int PostCopyItem(uint flags, nint item, nint destination, string name, int result, nint created) => 0;
        public int PreNewItem(uint flags, nint destination, string name) => 0;
        public int PostNewItem(uint flags, nint destination, string name, string template, uint attributes, int result, nint created) => 0;
        public int UpdateProgress(uint total, uint complete) => 0; public int ResetTimer() => 0; public int PauseTimer() => 0; public int ResumeTimer() => 0;
    }
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, out IShellItem item);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    private delegate nint SubclassProcedure(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowSubclass(nint window, SubclassProcedure procedure, nuint id, nuint data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(nint window, SubclassProcedure procedure, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
}
