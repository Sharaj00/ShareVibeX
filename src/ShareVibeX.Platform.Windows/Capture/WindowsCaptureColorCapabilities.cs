using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows.Capture;

public static class WindowsCaptureColorCapabilities
{
    private const uint ActivePaths = 2;
    private const int InsufficientBuffer = 122;

    public static void EnsureSdr(IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (!OperatingSystem.IsWindows()) throw UnknownColor();
        try { Validate(monitors, ReadColors()); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        { throw new CaptureSourceUnavailableException(Loc.T("Windows could not determine the capture display color mode. Select GDI or retry after checking display settings."), error); }
    }

    internal static void Validate(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<(string Source, uint? Flags)> colors)
    {
        foreach (var monitor in monitors)
        {
            var matches = colors.Where(color => string.Equals(color.Source, monitor.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0 || matches.Any(color => color.Flags is null || (color.Flags.Value & ~15u) != 0))
                throw UnknownColor();
            // A cloned source can drive several targets; every target must be SDR.
            if (matches.Any(color => (color.Flags!.Value & 2) != 0))
                throw new CaptureSourceUnavailableException(Loc.T("Windows Graphics Capture currently requires SDR displays. Disable HDR/advanced color or select GDI."));
        }
    }

    private static unsafe IReadOnlyList<(string Source, uint? Flags)> ReadColors()
    {
        // CCD sizes can change between calls during hot-plug; never retry indefinitely.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(ActivePaths, out var pathCount, out var modeCount) != 0 ||
                pathCount == 0 || pathCount > 16384 || modeCount > 32768) throw UnknownColor();
            var paths = new DisplayPath[pathCount];
            var modes = new DisplayMode[Math.Max(1u, modeCount)];
            var result = QueryDisplayConfig(ActivePaths, ref pathCount, paths, ref modeCount, modes, 0);
            if (result == InsufficientBuffer) continue;
            if (result != 0 || pathCount > paths.Length || modeCount > modes.Length) throw UnknownColor();
            var colors = new List<(string Source, uint? Flags)>();
            for (var index = 0; index < pathCount; index++)
            {
                var path = paths[index];
                var source = new SourceName { Header = new() { Type = 1, Size = 84, Adapter = path.SourceAdapter, Id = path.SourceId } };
                if (GetSourceName(ref source) != 0) throw UnknownColor();
                var name = ReadSourceName(source);
                if (name.Length == 0 || name.Contains('\0')) throw UnknownColor();
                var color = new AdvancedColor { Header = new() { Type = 9, Size = 32, Adapter = path.TargetAdapter, Id = path.TargetId } };
                var known = GetAdvancedColor(ref color) == 0 && color.Encoding <= 4 && color.BitsPerChannel is > 0 and <= 32;
                colors.Add((name, known ? color.Flags : null));
            }
            return colors;
        }
        throw UnknownColor();
    }

    private static CaptureSourceUnavailableException UnknownColor() => new(Loc.T("Windows could not determine the capture display color mode. Select GDI or retry after checking display settings."));

    internal static unsafe string ReadSourceName(SourceName source)=>new string(source.Name,0,32).TrimEnd('\0');

    // Explicit CCD ABI offsets include the full unused mode/target unions.
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    internal struct DisplayPath
    {
        [FieldOffset(0)] public long SourceAdapter;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(20)] public long TargetAdapter;
        [FieldOffset(28)] public uint TargetId;
    }
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DisplayMode { }
    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceHeader
    {
        public uint Type, Size;
        public uint AdapterLow;
        public int AdapterHigh;
        public uint Id;
        public long Adapter { readonly get => ((long)AdapterHigh << 32) | AdapterLow; set { AdapterLow = (uint)value; AdapterHigh = (int)(value >> 32); } }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal unsafe struct SourceName
    {
        public DeviceHeader Header;
        public fixed char Name[32];
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct AdvancedColor
    {
        public DeviceHeader Header;
        public uint Flags, Encoding, BitsPerChannel;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] DisplayPath[] paths, ref uint modeCount, [Out] DisplayMode[] modes, nint topology);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    private static extern int GetSourceName(ref SourceName source);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    private static extern int GetAdvancedColor(ref AdvancedColor color);
}
