using ShareVibeX.Core.Capture;

namespace ShareVibeX.Core.Recording;

public sealed record RecordingRequest(RecordingOptions Options, PixelRect Region, string OutputPath, bool WithAudio)
{
    public RecordingOptions? FinalAnimationOptions {get;init;}
    public CaptureBackend CaptureBackend {get;init;}=CaptureBackend.Gdi;
    public string? WorkingPath {get;init;}
    public RecordingSourceChoices? LiveSources {get;init;}
    public MonitorInfo? MovableMonitor {get;init;}
}
public interface IMovableRecordingVideo : IAsyncDisposable
{
    IReadOnlyList<string> Arguments { get; }
    Task Completion { get; }
    void Move(PixelRect region);
}
public sealed record RecordingResult(bool Success, string? OutputPath, string? PartialPath, string? Error);
public sealed record CapturePanelPlacement(PixelPoint Position,double Width,double Height);
public sealed record RecordingStartRequest(RecordingOptions Options,PixelRect Region,RecordingSourceChoices Sources)
{public CapturePanelPlacement? ToolbarPlacement {get;init;} public int CountdownSeconds {get;init;} public ShareVibeX.Core.Settings.SelectionOptions? OutlineOptions {get;init;} public MonitorInfo? MovableMonitor {get;init;}}
public sealed record RecordingProgress(TimeSpan? Duration, long? TotalBytes,long? Frames=null,bool IsEstimated=false)
{
    public static RecordingProgress Empty { get; } = new(null, null);
    public double? AverageBitrateKbps => Duration is { TotalSeconds: > 0 } time && TotalBytes is >= 0
        ? TotalBytes.Value * 8.0 / time.TotalSeconds / 1000 : null;
}
public interface IRecordingProcess : IAsyncDisposable
{
    RecordingProgress Progress => RecordingProgress.Empty;
    Task Ready { get; }
    Task<int> Exit { get; }
    string Diagnostics { get; }
    Task RequestStopAsync();
    void Kill();
}
public interface IRecordingProcessFactory
{
    IRecordingProcess Start(string executable, IReadOnlyList<string> arguments);
}

