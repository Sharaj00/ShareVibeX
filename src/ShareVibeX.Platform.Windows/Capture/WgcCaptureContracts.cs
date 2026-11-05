using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows.Capture;

public sealed record WgcCaptureTarget(MonitorInfo Monitor,nint Handle);
public sealed record WgcMonitorFrame(MonitorInfo Monitor,CapturedFrame Frame,TimeSpan Timestamp);

/// <summary>A request-owned monitor session. Readback cooperates with cancellation.</summary>
public interface IWgcMonitorSession:IDisposable
{
    event Action? FrameArrived;
    void Start(bool captureCursor);
    WgcMonitorFrame? TryReadFrame(CancellationToken cancellationToken);
}
