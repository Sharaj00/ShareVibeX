using ShareVibeX.Core.Capture;

namespace ShareVibeX.Core.Platform;

public interface IDesktopCapture
{
    IReadOnlyList<MonitorInfo> GetMonitors();
    Task<DesktopSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
