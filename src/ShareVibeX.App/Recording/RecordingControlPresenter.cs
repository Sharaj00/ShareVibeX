using Avalonia.Threading;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using ShareVibeX.App.Playback;

namespace ShareVibeX.App.Recording;

public sealed class RecordingControlPresenter : IDisposable
{
    private readonly RecordingService service;
    private readonly Func<IReadOnlyList<MonitorInfo>> monitors;
    private readonly Func<Func<Task>,Func<Task>,Action,RecordingControlWindow> factory;
    private readonly Func<nint,bool>? exclude;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(400)};
    private RecordingControlWindow? window;
    private readonly RecordingRegionFrame frame=new();
    private IReadOnlyList<MonitorInfo> layout=[];
    private bool disposed,refreshing;
    public Func<RecordingOptions,CancellationToken,Task<bool>>? MicrophoneProbe {get;init;}
    private bool microphoneAvailable=true;
    private DateTime nextMicrophoneCheck;
    private string? stopReason;
    private readonly IRecordingPreviewHost? preview;
    private bool previewOpened,preparingExit;
    private long previewGeneration;
    private CancellationTokenSource? previewSetup;
    private Task? previewOpening,previewClosing;
    public RecordingControlPresenter(RecordingService service,Func<IReadOnlyList<MonitorInfo>> monitors,
        Func<Func<Task>,Func<Task>,Action,RecordingControlWindow>? factory=null,Func<nint,bool>? exclude=null,IRecordingPreviewHost? preview=null)
    {
        this.service=service;this.monitors=monitors;this.factory=factory??((stop,cancel,close)=>new(stop,cancel,close));this.exclude=exclude;
        this.preview=preview;if(preview is not null)preview.Closed+=PreviewClosed;
        service.Changed+=Changed;timer.Tick+=async(_,_)=>await RefreshAsync();
    }
    public async Task StartAsync(RecordingStartRequest request,CancellationToken token)
    {
        if(request.Options.IsAnimation)request=request with{Sources=new(false,false)};
        if(disposed||preparingExit)throw new ObjectDisposedException(nameof(RecordingControlPresenter));
        if(service.IsActive||service.HasPendingResult)throw new InvalidOperationException(Loc.T("Close the current recording session first."));
        window?.Dismiss();window=null;
        ++previewGeneration;previewSetup?.Dispose();previewSetup=null;previewOpened=false;previewOpening=null;
        stopReason=null;
        layout=monitors().ToArray();
        var monitor=layout.OrderByDescending(m=>(long)m.Bounds.Intersect(request.Region).Width*m.Bounds.Intersect(request.Region).Height).FirstOrDefault()
            ??throw new InvalidOperationException(Loc.T("No capture monitor is available."));
        using var startingCancellation=CancellationTokenSource.CreateLinkedTokenSource(token);var starting=true;
        Task Stop(){if(starting){startingCancellation.Cancel();return service.IsActive?service.StopAsync():Task.CompletedTask;}return service.StopAsync();}
        Task Cancel(){if(starting){startingCancellation.Cancel();return service.IsActive?service.CancelAsync():Task.CompletedTask;}return service.CancelAsync();}
        var controls=factory(Stop,Cancel,()=>service.Session.ReleaseResult());window=controls;
        controls.ConfigureRecordingActions(service.TogglePauseAsync,async enabled=>
        {
            if(enabled&&MicrophoneProbe is not null&&!await MicrophoneProbe(request.Options,CancellationToken.None))
            {microphoneAvailable=false;Update();throw new InvalidOperationException(Loc.T(MicrophoneAvailability.UnavailableMessage));}
            await service.SetMicrophoneEnabledAsync(enabled);
        });
        try
        {
            microphoneAvailable=!request.Options.IsAnimation&&(MicrophoneProbe is null||await MicrophoneProbe(request.Options,startingCancellation.Token));
            controls.SetMicrophoneAvailable(microphoneAvailable,request.Options.IsAnimation);
            if(request.Sources.Microphone&&!microphoneAvailable)throw new InvalidOperationException(Loc.T(MicrophoneAvailability.UnavailableMessage));
            var movable=!request.Options.UseCustomCommand&&request.Region!=monitor.Bounds&&monitor.Bounds.Intersect(request.Region)==request.Region;
            if(!controls.TryPresent(request.Region,monitor,exclude,request.ToolbarPlacement,roomForMove:true))
                throw new InvalidOperationException(Loc.T("Recording controls cannot be excluded from capture or placed outside the region. Choose a smaller region or use a Windows configuration supporting capture exclusion."));
            Func<PixelRect,PixelRect?>? move=null;
            if(controls.IsCaptureExcluded&&movable)
                move=region=>
                {
                    var clamped=region with
                    {
                        X=Math.Clamp(region.X,monitor.Bounds.X,monitor.Bounds.Right-region.Width),
                        Y=Math.Clamp(region.Y,monitor.Bounds.Y,monitor.Bounds.Bottom-region.Height)
                    };
                    return service.Session.TryMoveRegion(clamped)?clamped:null;
                };
            var showFrame=request.OutlineOptions?.ShowRecordingOutline!=false;
            if(showFrame)frame.Show(request.Region,layout,exclude,options:request.OutlineOptions);
            if(move is not null&&(!showFrame||frame.Windows.Any(border=>border.IsVisible)))
            {

                controls.ConfigureRegionMove(()=>service.Session.Region,(start,dx,dy)=>
                {
                    if(move(start with{X=start.X+dx,Y=start.Y+dy}) is not {} accepted)return;
                    frame.MoveTo(accepted);
                    controls.FollowRegion(accepted,monitor);
                });
                request=request with{MovableMonitor=monitor};
            }
            startingCancellation.Token.ThrowIfCancellationRequested();
            await service.StartAsync(request,startingCancellation.Token);
            if(service.Session.CanMoveRegion)controls.FollowRegion(service.Session.Region,monitor);
            starting=false;Update();timer.Start();
        }
        catch{frame.Dispose();controls.Dismiss();if(ReferenceEquals(window,controls))window=null;throw;}
    }
    public void Activate(){if(previewOpened)preview?.Activate();else window?.Activate();}
    public async Task RecoverAsync(ShareVibeX.App.Recovery.RecoverableRecording recording,string? trustedFfmpegPath)
    {
        if(disposed||preparingExit)throw new InvalidOperationException(Loc.T("The application is closing."));
        if(service.IsActive||service.HasPendingResult)throw new InvalidOperationException(Loc.T("Close the current recording session first."));
        ++previewGeneration;previewOpened=false;previewOpening=null;previewSetup?.Dispose();previewSetup=null;stopReason=null;
        if(recording.Options is null||recording.Media is null)throw new IOException(Loc.T("This session is available for manual recovery only."));
        recording=recording with{Options=recording.Options with{FfmpegPath=trustedFfmpegPath},Media=recording.Media with{FfmpegPath=trustedFfmpegPath}};
        service.Session.Restore(recording);if(previewOpening is not null)await previewOpening;
    }
    private void PreviewClosed()
    {
        ++previewGeneration;previewOpened=false;
        if(service.HasPendingResult)service.Session.ReleaseResult();
    }
    private void Changed()
    {
        if(Dispatcher.UIThread.CheckAccess())Update();else Dispatcher.UIThread.Post(Update);
    }
    private void Update()
    {
        if(disposed)return;
        var session=service.Session;
        window?.SetRegionMoveAvailable(session.CanMoveRegion);
        if(!service.IsActive)frame.Dispose();
        if(session.State==RecordingSessionState.Idle){window?.Dismiss();window=null;timer.Stop();return;}
        var detail=session.Result?.OutputPath??session.Result?.PartialPath??(session.State==RecordingSessionState.Failed?session.Result?.Error:null);
        if(session.LastError is {} error)detail=error+(detail is null?"":" · "+detail);
        if(stopReason is not null)detail=stopReason+(detail is null?"":" · "+detail);
        window?.SetCountdown(session.CountdownRemaining);
        window?.Update(session.State,session.Progress,session.Width,session.Height,detail,session.CanPause,session.CanToggleMicrophone,session.Sources.Microphone);
        window?.SetMicrophoneAvailable(microphoneAvailable,service.IsActive&&service.Session.Options.IsAnimation);
        if(session.HasPendingResult&&preview is not null&&!previewOpened&&!preparingExit)
        {
            previewOpened=true;previewSetup=new();var version=++previewGeneration;
            previewOpening=OpenPreviewAsync(version,previewSetup.Token);
        }
        if(!service.IsActive)timer.Stop();
    }
    private async Task OpenPreviewAsync(long version,CancellationToken token)
    {
        try
        {
            await preview!.ShowEditableAsync(service.Session.CreatePreviewMedia(),service.Session.Region,monitors(),service.Session.Workspace!,service.Session.Options,token);
            if(!disposed&&!preparingExit&&version==previewGeneration){window?.Dismiss();window=null;}
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception error){if(!disposed&&version==previewGeneration){stopReason=Loc.T("Preview failed: ")+error.Message;window?.Update(service.Session.State,service.Session.Progress,service.Session.Width,service.Session.Height,stopReason);}}
    }
    public Task ClosePreviewAsync()=>previewClosing??=ClosePreviewCoreAsync();
    public async Task<bool> RequestExitAsync()
    {
        if(previewOpening is {IsCompleted:false})previewSetup?.Cancel();
        if(previewOpening is not null)await previewOpening;
        if(preview is not null&&!await preview.RequestCloseAsync())return false;
        await ClosePreviewAsync();return true;
    }
    private async Task ClosePreviewCoreAsync()
    {
        preparingExit=true;++previewGeneration;previewSetup?.Cancel();
        if(preview is not null)await preview.CloseAsync();
        if(previewOpening is not null)await previewOpening;
    }
    public async Task RefreshAsync()
    {
        if(disposed||refreshing)return;refreshing=true;
        try
        {
            if(service.IsActive&&MicrophoneProbe is not null&&DateTime.UtcNow>=nextMicrophoneCheck&&service.Session.Options is {IsAnimation:false} options)
            {microphoneAvailable=await MicrophoneProbe(options,CancellationToken.None);nextMicrophoneCheck=DateTime.UtcNow.AddSeconds(2);if(disposed)return;}
            if(service.IsActive && !monitors().SequenceEqual(layout))
            {
                stopReason=Loc.T("The display layout changed. Recording stopped.");
                await service.StopAsync();
                DiagnosticLog.Write(Loc.T("Recording stopped because display topology changed."));
            }
            Update();
        }
        catch(Exception error)
        {
            stopReason=Loc.T("Recording controls failed: ")+error.Message;
            try{await service.StopAsync();}catch(Exception stopError){DiagnosticLog.Write(Loc.T("Recording stop failed: ")+stopError.Message);}
            DiagnosticLog.Write(Loc.T("Recording controls failed: ")+error.Message);Update();
        }
        finally{refreshing=false;}
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;frame.Dispose();timer.Stop();service.Changed-=Changed;window?.Dismiss();window=null;
        if(preview is not null)preview.Closed-=PreviewClosed;
        previewSetup?.Cancel();_=ObserveCloseAsync();
    }
    private async Task ObserveCloseAsync(){try{await ClosePreviewAsync();}catch(Exception error){DiagnosticLog.Write(Loc.T("Preview close failed: ")+error.Message);}finally{previewSetup?.Dispose();}}
}



