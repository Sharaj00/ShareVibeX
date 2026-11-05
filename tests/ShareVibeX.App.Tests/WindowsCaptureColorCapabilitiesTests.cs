using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows.Capture;
using Xunit;

namespace ShareVibeX.App.Tests;

public sealed class WindowsCaptureColorCapabilitiesTests
{
    [Fact]
    public void NativeSourceNamePreservesUtf16Characters()
    {
        var memory=Marshal.AllocHGlobal(84);
        try
        {
            var bytes=new byte[84];
            System.Text.Encoding.Unicode.GetBytes(@"\\.\DISPLAY1").CopyTo(bytes,20);
            Marshal.Copy(bytes,0,memory,bytes.Length);
            var source=Marshal.PtrToStructure<WindowsCaptureColorCapabilities.SourceName>(memory);
            Assert.Equal(@"\\.\DISPLAY1",WindowsCaptureColorCapabilities.ReadSourceName(source));
        }
        finally{Marshal.FreeHGlobal(memory);}
    }
    private static readonly MonitorInfo[] Monitors = [new("DISPLAY1", new(0, 0, 10, 10), 1)];

    [Fact]
    public void SdrSupportedOrUnsupportedAndCaseInsensitiveNamesAreAccepted()
    {
        WindowsCaptureColorCapabilities.Validate(Monitors, [("display1", 0u)]);
        WindowsCaptureColorCapabilities.Validate(Monitors, [("DISPLAY1", 1u)]);
    }

    [Theory]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(16u)]
    [InlineData(null)]
    public void EnabledOrUnknownColorIsUnavailable(uint? flags) =>
        Assert.Throws<CaptureSourceUnavailableException>(() => WindowsCaptureColorCapabilities.Validate(Monitors, [("DISPLAY1", flags)]));

    [Fact]
    public void MissingMonitorAndHdrCloneAreUnavailable()
    {
        Assert.Throws<CaptureSourceUnavailableException>(() => WindowsCaptureColorCapabilities.Validate(Monitors, [("DISPLAY2", 0u)]));
        Assert.Throws<CaptureSourceUnavailableException>(() => WindowsCaptureColorCapabilities.Validate(Monitors, [("DISPLAY1", 0u), ("DISPLAY1", 3u)]));
    }

    [Fact]
    public void CcdStructuresMatchWindowsAbiWithoutCallingWindows()
    {
        Assert.Equal(72, Marshal.SizeOf<WindowsCaptureColorCapabilities.DisplayPath>());
        Assert.Equal(64, Marshal.SizeOf<WindowsCaptureColorCapabilities.DisplayMode>());
        Assert.Equal(20, Marshal.SizeOf<WindowsCaptureColorCapabilities.DeviceHeader>());
        Assert.Equal(84, Marshal.SizeOf<WindowsCaptureColorCapabilities.SourceName>());
        Assert.Equal(32, Marshal.SizeOf<WindowsCaptureColorCapabilities.AdvancedColor>());
        Assert.Equal((nint)20, Marshal.OffsetOf<WindowsCaptureColorCapabilities.DisplayPath>(nameof(WindowsCaptureColorCapabilities.DisplayPath.TargetAdapter)));
    }
}
