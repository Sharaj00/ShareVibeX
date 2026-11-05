using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Recording;

public sealed class RecordingService
{
    private readonly Func<CaptureBackend> preference;
    public RecordingSessionController Session {get;}
    public bool IsActive=>Session.IsActive;
    public bool HasPendingResult=>Session.HasPendingResult;
    public string Status=>(Session.State switch
    {
        RecordingSessionState.Starting=>Loc.T("Starting recording…"),
        RecordingSessionState.Pausing=>Loc.T("Pausing recording…"),
        RecordingSessionState.Paused=>Loc.T("Paused · ")+(Session.Progress.Duration?.ToString(@"hh\:mm\:ss")??"—"),
        RecordingSessionState.Resuming=>Loc.T("Resuming recording…"),
        RecordingSessionState.Finalizing=>Loc.T("Finalizing recording…"),
        RecordingSessionState.Result=>Loc.T("Recording retained: ")+Session.Result?.OutputPath,
        _=>Loc.T("Recording · ")+(Session.Progress.Duration?.ToString(@"hh\:mm\:ss")??"—")
    })+SourceStatus;
    private string SourceStatus=>Session.EffectiveCaptureBackend is not {} method?"":
        " · "+(Session.Options.UseCustomCommand?Loc.T("Custom FFmpeg source"):
        method==CaptureBackend.WindowsGraphicsCapture?"WGC":Session.CaptureFallbackReason is {} reason?Loc.F("GDI (WGC unavailable: {0})",reason):"GDI");
    public event Action? Changed;
    public event Action<RecordingSessionOutcome>? Finished;
    public RecordingService(RecordingEngine engine,string? root=null,Func<CaptureBackend>? preference=null)
    {
        this.preference=preference??(()=>CaptureBackend.Gdi);
        Session=new(engine,root??Path.Combine(AppContext.BaseDirectory,"data","recordings"));
        Session.Changed+=()=>Changed?.Invoke();Session.Finished+=outcome=>Finished?.Invoke(outcome);
    }
    public Task StartAsync(RecordingStartRequest request,CancellationToken token)=>Session.StartAsync(request.Options,request.Region,request.Sources,token,request.CountdownSeconds,preference(),request.MovableMonitor);
    public Task StopAsync()=>Session.StopAsync();
    public Task CancelAsync()=>Session.CancelAsync();
    public Task TogglePauseAsync()=>Session.State==RecordingSessionState.Paused?Session.ResumeAsync():Session.PauseAsync();
    public Task SetMicrophoneEnabledAsync(bool enabled)=>Session.SetMicrophoneEnabledAsync(enabled);
    public static string Describe(RecordingResult result)=>result.Success?Loc.T("Recording retained: ")+result.OutputPath:
        Short(result.Error??Loc.T("Recording failed."))+(result.PartialPath is null?"":Loc.T(" Recoverable partial: ")+result.PartialPath);
    private static string Short(string value)=>value.Length<=1200?value:value[..1200]+"…";
}

