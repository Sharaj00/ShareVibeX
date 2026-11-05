namespace ShareVibeX.Core.Capture;

public sealed record MonitorInfo(string Id, PixelRect Bounds, double Scale);
public sealed record CapturedFrame(int Width, int Height, int Stride, byte[] Bgra32);
public sealed record DesktopSnapshot(PixelRect Bounds, IReadOnlyList<MonitorInfo> Monitors, CapturedFrame Frame);
public enum CaptureMode { Screenshot }
public enum CaptureState { Idle, Selecting, Ready }
public sealed record CaptureSession(DesktopSnapshot Snapshot, PixelRect Region, CaptureMode Mode = CaptureMode.Screenshot);
