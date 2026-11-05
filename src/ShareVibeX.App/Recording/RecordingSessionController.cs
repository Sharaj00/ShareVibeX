using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Playback;
using ShareVibeX.App.Recovery;

namespace ShareVibeX.App.Recording;

public enum RecordingSessionState { Idle, Starting, Recording, Pausing, Paused, Resuming, Finalizing, Result, Failed }
public sealed record RecordingSessionOutcome(RecordingWorkspace? Workspace,RecordingResult? Result,bool Canceled);

public sealed class RecordingSessionController
{
    private readonly RecordingEngine engine;
    private readonly string root;
    private readonly IRecordingAssembler assembler;
    private readonly object gate=new();
    private readonly SemaphoreSlim transition=new(1);
    private readonly List<RecordingSegment> segments=[];
    private RecordingRun? run;
    private RecordingOptions? options;
    private CaptureBackend captureBackend;
    private MonitorInfo? movableMonitor;
    public bool CanMoveRegion=>State==RecordingSessionState.Recording&&!requestedStop&&run?.CanMoveRegion==true;
    public CaptureBackend? EffectiveCaptureBackend {get;private set;}
    public string? CaptureFallbackReason {get;private set;}
    private string? segmentWorking;
    private bool segmentHasAudio,everAudio,published;
    private volatile bool discard,requestedStop;
    private CancellationTokenSource? startup,assemblyCancellation,controlsLifetime;
    private TaskCompletionSource? startFinished;
    private TaskCompletionSource<RecordingResult>? completed;
    private Task? stopping,canceling;
    public RecordingSessionController(RecordingEngine engine,string root,IRecordingAssembler? assembler=null)
    {this.engine=engine;this.root=root;this.assembler=assembler??engine;}
    public RecordingSessionState State {get;private set;}
    public bool IsActive=>State is RecordingSessionState.Starting or RecordingSessionState.Recording or RecordingSessionState.Pausing
        or RecordingSessionState.Paused or RecordingSessionState.Resuming or RecordingSessionState.Finalizing || startFinished?.Task.IsCompleted==false;
    public bool HasPendingResult=>State==RecordingSessionState.Result;
    public bool CanPause=>options?.UseCustomCommand==false && State is RecordingSessionState.Recording or RecordingSessionState.Paused;
    public bool CanToggleMicrophone=>options?.IsAnimation!=true && engine.SupportsLiveAudio && options?.AudioSource!=AudioSource.DirectShow && State is RecordingSessionState.Recording or RecordingSessionState.Paused;
    public RecordingSourceChoices Sources {get;private set;}
    public PixelRect Region {get;private set;}
    public RecordingWorkspace? Workspace {get;private set;}
    public RecordingOptions Options=>options??throw new InvalidOperationException(Loc.T("No recording settings are available."));
    public void Restore(RecoverableRecording recording)
    {
        if(IsActive||HasPendingResult)throw new InvalidOperationException(Loc.T("Close the current recording session first."));
        if(recording.Media is not {} media||recording.Workspace is null||recording.Options is null)throw new IOException(Loc.T("This session is available for manual recovery only."));
        options=recording.Options;EffectiveCaptureBackend=null;CaptureFallbackReason=null;Workspace=recording.Workspace;Region=recording.Region;Width=media.Width;Height=media.Height;everAudio=media.HasAudio;
        segments.Clear();segments.AddRange(recording.Segments);run=null;Result=new(true,media.Path,null,null);LastError=null;published=true;
        State=RecordingSessionState.Result;Changed?.Invoke();
    }
    public RecordingResult? Result {get;private set;}
    public string? LastError {get;private set;}
    public IReadOnlyList<RecordingSegment> Segments=>segments.AsReadOnly();
    public Task<RecordingResult>? Completion=>completed?.Task;
    public PreviewMedia CreatePreviewMedia()
    {
        if(!HasPendingResult||Result?.OutputPath is not {} path||options is null)throw new InvalidOperationException(Loc.T("No completed recording is available for preview."));
        return new(path,options.FfmpegPath,RecordedDuration(),Width,Height,options.FrameRate,everAudio);
    }
    public int Width {get;private set;}
    public int Height {get;private set;}
    public RecordingProgress Progress
    {
        get
        {
            var duration=RecordedDuration();long? bytes=segments.Sum(s=>s.TotalBytes);var estimated=false;
            if(run is {} current)
            {
                var progress=current.Progress;var active=MediaDuration(progress);
                duration=duration is {} a && active is {} b?a+b:null;
                long? fileBytes=null;
                try{if(segmentWorking is not null&&File.Exists(segmentWorking))fileBytes=new FileInfo(segmentWorking).Length;}catch(IOException){}catch(UnauthorizedAccessException){}
                var displayed=RecordingStatistics.Resolve(progress with{Duration=active},fileBytes,Options,Width,Height,Sources.WithAudio);
                bytes=displayed.TotalBytes is {} size?bytes+size:null;estimated=displayed.IsEstimated;
            }
            if(State==RecordingSessionState.Result && Result?.OutputPath is {} path)
                try{bytes=new FileInfo(path).Length;}catch(IOException){}
            return new(duration,bytes,IsEstimated:estimated);
        }
    }
    public event Action? Changed;
    public event Action<RecordingSessionOutcome>? Finished;
    public int CountdownRemaining {get;private set;}

    public async Task StartAsync(RecordingOptions value,PixelRect region,RecordingSourceChoices sources,CancellationToken token=default,int countdownSeconds=0,CaptureBackend captureBackend=CaptureBackend.Gdi,MonitorInfo? movableMonitor=null)
    {
        if(!Enum.IsDefined(captureBackend))throw new ArgumentOutOfRangeException(nameof(captureBackend));
        if(value.IsAnimation)sources=new(false,false);
        value=sources.Apply(value);var startingDone=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(gate)
        {
            if(IsActive||HasPendingResult||canceling is {IsCompleted:false})throw new InvalidOperationException(Loc.T("Close the current recording session before starting another."));
            controlsLifetime?.Dispose();controlsLifetime=new();
            options=value;this.captureBackend=captureBackend;this.movableMonitor=movableMonitor;EffectiveCaptureBackend=null;CaptureFallbackReason=null;Sources=sources;Region=region;Workspace=null;Result=null;LastError=null;run=null;segments.Clear();Width=Height=0;
            completed=new(TaskCreationOptions.RunContinuationsAsynchronously);discard=requestedStop=published=everAudio=false;stopping=canceling=null;startFinished=startingDone;
            State=RecordingSessionState.Starting;
        }
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token,controlsLifetime.Token);lock(gate)startup=cancel;
        using var countdownCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
        Task countdown=Task.CompletedTask;
        Changed?.Invoke();await transition.WaitAsync();
        try
        {
            cancel.Token.ThrowIfCancellationRequested();Workspace=RecordingWorkspace.Create(root,value,region);
            countdown=CountDownAsync(Math.Clamp(countdownSeconds,0,10),countdownCancellation.Token);
            await StartSegmentAsync(cancel.Token,countdown);
            State=RecordingSessionState.Recording;Changed?.Invoke();
        }
        catch(OperationCanceledException)when(cancel.IsCancellationRequested)
        {
            try{if(Workspace is not null)await Workspace.DiscardAsync();}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException){throw CleanupFailed(error);}
            State=RecordingSessionState.Idle;Complete(new(false,null,null,Loc.T("Recording canceled.")),true);throw;
        }
        catch(Exception error)
        {
            var result=Failure(error);await SaveManifestAsync("Failed",result);Complete(result,false);throw;
        }
        finally
        {
            countdownCancellation.Cancel();
            try{await countdown;}catch(OperationCanceledException)when(countdownCancellation.IsCancellationRequested){}
            transition.Release();
            lock(gate){if(ReferenceEquals(startup,cancel))startup=null;startingDone.TrySetResult();}
            Changed?.Invoke();
        }
    }
    private async Task CountDownAsync(int seconds,CancellationToken token)
    {
        var start=System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            while(TimeSpan.FromSeconds(seconds)-System.Diagnostics.Stopwatch.GetElapsedTime(start) is {Ticks:>0} remaining)
            {
                CountdownRemaining=(int)Math.Ceiling(remaining.TotalSeconds);
                Changed?.Invoke();
                await Task.Delay(remaining-TimeSpan.FromSeconds(CountdownRemaining-1),token);
            }
        }
        finally{CountdownRemaining=0;Changed?.Invoke();}
    }
    private async Task StartSegmentAsync(CancellationToken token,Task? startGate=null)
    {
        var remaining=options!.DurationSeconds>0?Math.Max(0,options.DurationSeconds-(RecordedDuration()?.TotalSeconds??0)):0;
        var currentOptions=Sources.Apply(options.CaptureOptions()) with{DurationSeconds=remaining};
        var live=!options.IsAnimation && engine.SupportsLiveAudio && currentOptions.AudioSource!=AudioSource.DirectShow;
        var paths=Workspace!.CreateSegment(currentOptions.Extension);segmentWorking=paths.Working;
        await Workspace.WriteManifestAsync(State.ToString(),token:token,segments:segments,currentRegion:Region);
        var started=await engine.StartAsync(new(currentOptions,Region,paths.Capture,live||Sources.WithAudio)
            {WorkingPath=paths.Working,LiveSources=live?Sources:null,FinalAnimationOptions=options.IsAnimation?options:null,CaptureBackend=captureBackend,MovableMonitor=movableMonitor},token,startGate);
        captureBackend=started.EffectiveCaptureBackend;
        EffectiveCaptureBackend=captureBackend;CaptureFallbackReason??=started.CaptureFallbackReason;
        run=started;segmentHasAudio=live||Sources.WithAudio;everAudio|=Sources.WithAudio;Width=started.Width;Height=started.Height;LastError=null;
        _=WatchRunAsync(started);
    }
    public async Task PauseAsync()
    {
        await transition.WaitAsync();
        try{await PauseCoreAsync();}
        finally{transition.Release();}
    }
    private async Task PauseCoreAsync()
    {
        if(State!=RecordingSessionState.Recording||requestedStop)return;
        if(options!.UseCustomCommand)throw new NotSupportedException(Loc.T("Pause requires structured FFmpeg encoding settings."));
        State=RecordingSessionState.Pausing;Changed?.Invoke();
        if(!await ConsumeRunAsync())return;
        if(LimitReached()||requestedStop){await FinalizeAsync();return;}
        State=RecordingSessionState.Paused;await SaveManifestAsync("Paused");Changed?.Invoke();
    }
    public async Task ResumeAsync(CancellationToken token=default)
    {
        await transition.WaitAsync(token);
        try{await ResumeCoreAsync(token);}
        finally{transition.Release();}
    }
    public bool TryMoveRegion(PixelRect target)
    {
        if(!transition.Wait(0))return false;
        try
        {
            if(!CanMoveRegion)return false;
            if(target==Region)return true;
            run!.MoveRegion(target);
            Region=target;
            return true;
        }
        finally{transition.Release();}
    }
    private async Task ResumeCoreAsync(CancellationToken token)
    {
        if(State!=RecordingSessionState.Paused||requestedStop)return;
        if(LimitReached()){await FinalizeAsync();return;}
        if(segments.Any(s=>s.Duration is null))throw new InvalidDataException(Loc.T("Recorded duration is unavailable. Stop and retain the current segments."));
        State=RecordingSessionState.Resuming;Changed?.Invoke();
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token,controlsLifetime!.Token);lock(gate)startup=cancel;
        try{await StartSegmentAsync(cancel.Token);State=RecordingSessionState.Recording;Changed?.Invoke();}
        catch(Exception error){State=RecordingSessionState.Paused;LastError=error.Message;Changed?.Invoke();throw;}
        finally{lock(gate)if(ReferenceEquals(startup,cancel))startup=null;}
    }
    public async Task SetMicrophoneEnabledAsync(bool enabled,CancellationToken token=default)
    {
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token,controlsLifetime?.Token??default);
        await transition.WaitAsync(cancel.Token);
        try
        {
            if(!CanToggleMicrophone)throw new NotSupportedException(Loc.T("Live microphone control is unavailable for this source or state."));
            if(Sources.Microphone==enabled)return;
            if(State==RecordingSessionState.Recording){await run!.SetMicrophoneEnabledAsync(enabled,cancel.Token);everAudio|=enabled;}
            Sources=Sources with{Microphone=enabled};LastError=null;Changed?.Invoke();
        }
        catch(Exception error){LastError=error.Message;Changed?.Invoke();throw;}
        finally{transition.Release();}
    }
    public Task StopAsync()
    {
        lock(gate)
        {
            requestedStop=true;
            if(State==RecordingSessionState.Starting)discard=true;
            startup?.Cancel();return stopping??=StopCoreAsync();
        }
    }
    private async Task StopCoreAsync()
    {
        await Task.Yield();await transition.WaitAsync();
        try
        {
            if(completed?.Task.IsCompleted==true)return;
            State=RecordingSessionState.Finalizing;Changed?.Invoke();
            if(run is not null && !await ConsumeRunAsync())return;
            if(discard){Complete(new(false,null,null,Loc.T("Recording canceled.")),true);return;}
            await FinalizeAsync();
        }
        finally{transition.Release();}
    }
    private async Task<bool> ConsumeRunAsync()
    {
        var active=run;if(active is null)return true;
        var result=await active.StopAsync();run=null;
        if(!result.Success){await SaveManifestAsync("Failed",result);Complete(result,discard);return false;}
        var bytes=new FileInfo(result.OutputPath!).Length;
        segments.Add(new(result.OutputPath!,MediaDuration(active.Progress),bytes,segmentHasAudio,active.Width,active.Height));
        await SaveManifestAsync(State.ToString());return true;
    }
    private async Task WatchRunAsync(RecordingRun observed)
    {
        await observed.Completion;await transition.WaitAsync();
        try
        {
            if(!ReferenceEquals(run,observed))return;
            if(!await ConsumeRunAsync())return;
            if(discard){Complete(new(false,null,null,Loc.T("Recording canceled.")),true);return;}
            await FinalizeAsync();
        }
        catch(Exception error){Complete(Failure(error),discard);}
        finally{transition.Release();}
    }
    private async Task FinalizeAsync()
    {
        if(completed?.Task.IsCompleted==true)return;
        State=RecordingSessionState.Finalizing;Changed?.Invoke();
        if(segments.Count==0){Complete(new(false,null,null,Loc.T("No recorded segments are available.")),discard);return;}
        using var cancel=new CancellationTokenSource();lock(gate){assemblyCancellation=cancel;if(discard)cancel.Cancel();}
        RecordingResult result;
        try{result=await assembler.AssembleAsync(options!,Workspace!,segments.ToArray(),everAudio,cancel.Token);}
        catch(OperationCanceledException)when(cancel.IsCancellationRequested){result=new(false,null,segments[0].Path,Loc.T("Recording canceled."));}
        catch(Exception error){result=Failure(error);}
        finally{lock(gate)assemblyCancellation=null;}
        await SaveManifestAsync(result.Success?"Result":"Failed",result);Complete(result,discard);
    }
    public Task CancelAsync()
    {
        lock(gate)
        {
            discard=true;controlsLifetime?.Cancel();startup?.Cancel();assemblyCancellation?.Cancel();
            if(canceling is {IsFaulted:true} or {IsCanceled:true})canceling=null;
            return canceling??=CancelCoreAsync();
        }
    }
    private async Task CancelCoreAsync()
    {
        await Task.Yield();await StopAsync();await transition.WaitAsync();
        try
        {
            State=RecordingSessionState.Finalizing;Changed?.Invoke();
            try{if(Workspace is not null && Directory.Exists(Workspace.DirectoryPath))await Workspace.DiscardAsync();}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException){throw CleanupFailed(error);}
            State=RecordingSessionState.Idle;Result=null;LastError=null;Changed?.Invoke();
        }
        finally{transition.Release();}
    }
    private IOException CleanupFailed(Exception error)
    {
        State=RecordingSessionState.Failed;var message=Loc.T("Could not discard the recording. Files remain in ")+Workspace?.DirectoryPath+Loc.T(". Release file locks and retry Cancel. ")+error.Message;
        Result=new(false,null,segmentWorking,message);LastError=message;Changed?.Invoke();return new IOException(message,error);
    }
    private RecordingResult Failure(Exception error)=>new(false,null,segmentWorking is not null && File.Exists(segmentWorking)?segmentWorking:segments.FirstOrDefault()?.Path,error.Message);
    private async Task SaveManifestAsync(string state,RecordingResult? result=null)
    {
        if(Workspace is null)return;
        try{await Workspace.WriteManifestAsync(state,result,segments:segments,hasAudio:everAudio,currentRegion:Region);}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){LastError=Loc.T("Recovery metadata could not be updated: ")+error.Message;DiagnosticLog.Write(LastError);}
    }
    private void Complete(RecordingResult result,bool canceled)
    {
        Result=result;if(!canceled)State=result.Success?RecordingSessionState.Result:RecordingSessionState.Failed;
        if(published)return;
        published=true;
        if(!canceled&&!result.Success)DiagnosticLog.Write("Recording failed: "+result.Error);
        Changed?.Invoke();Finished?.Invoke(new(Workspace,result,canceled));completed?.TrySetResult(result);
    }
    private TimeSpan? MediaDuration(RecordingProgress progress)=>progress.Frames is >0 && options is not null
        ? TimeSpan.FromSeconds(progress.Frames.Value/(double)options.FrameRate):progress.Duration;
    private TimeSpan? RecordedDuration()=>segments.Any(s=>s.Duration is null)?null:TimeSpan.FromTicks(segments.Sum(s=>s.Duration!.Value.Ticks));
    private bool LimitReached()=>options?.DurationSeconds>0 && RecordedDuration()?.TotalSeconds>=options.DurationSeconds;
    public void ReleaseResult(){if(IsActive)throw new InvalidOperationException(Loc.T("Stop before closing the result."));State=RecordingSessionState.Idle;Changed?.Invoke();}
}


