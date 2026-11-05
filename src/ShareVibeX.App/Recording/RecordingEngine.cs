using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Recording;

public sealed class RecordingEngine(ICommandRunner probeRunner, IRecordingProcessFactory processes, IRecordingAudioFactory audio,
    Func<string?, string>? resolve = null, TimeSpan? startTimeout = null, TimeSpan? stopTimeout = null,
    Func<PixelRect,RecordingOptions,WgcVideoSource>? resolveWgc=null,
    Func<PixelRect,MonitorInfo,RecordingOptions,CancellationToken,IMovableRecordingVideo>? openMovableVideo=null,
    Func<PixelRect,MonitorInfo,RecordingOptions,CancellationToken,IMovableRecordingVideo>? openMovableWgc=null):IRecordingAssembler
{
    public bool SupportsLiveAudio=>audio is ILiveRecordingAudioFactory;
    public Task<RecordingResult> AssembleAsync(RecordingOptions options,RecordingWorkspace workspace,IReadOnlyList<RecordingSegment> segments,bool keepAudio,CancellationToken token)=>new RecordingAssembler(processes,resolve).AssembleAsync(options,workspace,segments,keepAudio,token);
    private int occupied;
    public async Task<RecordingRun> StartAsync(RecordingRequest request, CancellationToken token = default,Task? startGate=null)
    {
        if(Interlocked.Exchange(ref occupied,1)!=0)throw new InvalidOperationException(Loc.T("A recording is already active."));
        var lifetime=new CancellationTokenSource();
        IRecordingAudioSource? source=null;IMovableRecordingVideo? video=null;IRecordingProcess? process=null;RecordingRun? run=null;
        using var startupCancellation=token.Register(()=>{try{lifetime.Cancel();}catch(ObjectDisposedException){ }});
        try
        {
            var options=request.Options;
            if(!Enum.IsDefined(request.CaptureBackend))throw new ArgumentOutOfRangeException(nameof(request.CaptureBackend));
            if(request.LiveSources is not null && (!request.WithAudio||audio is not ILiveRecordingAudioFactory ||options.AudioSource==AudioSource.DirectShow))
                throw new NotSupportedException(Loc.T("Live microphone control requires a native live audio stream."));
            var target=Path.GetFullPath(request.OutputPath);
            if(!string.Equals(Path.GetExtension(target),"."+options.Extension,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(Loc.T("The recording filename must match the selected container."));
            if(request.WithAudio && options.AudioSource==AudioSource.DirectShow && string.IsNullOrWhiteSpace(options.DirectShowDevice))
                throw new ArgumentException(Loc.T("Select the exact DirectShow audio device name in Video settings."));
            var executable=(resolve??(value=>FfmpegLocator.Resolve(value,ShareVibeX.Core.IO.PortablePaths.BaseDirectory)))(options.FfmpegPath);
            IReadOnlyList<string> placeholder=["-f","f32le","-ar","48000","-ac","2","-i",@"\\.\pipe\pending"];
            if(options.AudioSource==AudioSource.DirectShow)placeholder=["-thread_queue_size","1024","-f","dshow","-i","audio="+options.DirectShowDevice];
            var directory=Path.GetDirectoryName(target)!;
            var temporary=request.WorkingPath is null
                ? Path.Combine(directory,Path.GetFileNameWithoutExtension(target)+"."+Guid.NewGuid().ToString("N")+".partial."+options.Extension)
                : Path.GetFullPath(request.WorkingPath);
            if(!string.Equals(Path.GetDirectoryName(temporary),directory,StringComparison.OrdinalIgnoreCase) ||
                string.Equals(temporary,target,StringComparison.OrdinalIgnoreCase) || File.Exists(temporary) ||
                !string.Equals(Path.GetExtension(temporary),"."+options.Extension,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(Loc.T("A fresh working file beside the recording destination is required."));
            var preview=FfmpegCommandBuilder.Build(options,request.Region,request.WithAudio,temporary,placeholder);
            var capabilities=(await new FfmpegProbe(probeRunner).TestAsync(executable,token)).Capabilities;
            if(!options.UseCustomCommand)
            {
                if(!capabilities.Encoders.Contains(options.EncoderName))
                    throw new InvalidOperationException(Loc.F("The selected FFmpeg does not include encoder {0}. Choose another encoder or FFmpeg build.",options.EncoderName));
                await new FfmpegEncoderProbe(probeRunner).ValidateAsync(executable,options,token);
            }
            if(request.FinalAnimationOptions is {} finalOptions)
            {
                if(!finalOptions.IsAnimation||options.Encoder!=VideoEncoder.Ffv1||request.WithAudio)throw new ArgumentException(Loc.T("Invalid animation capture pipeline."));
                if(!capabilities.Encoders.Contains(finalOptions.EncoderName))throw new InvalidOperationException(Loc.F("The selected FFmpeg does not include encoder {0}. Choose another encoder or FFmpeg build.",finalOptions.EncoderName));
                await new FfmpegEncoderProbe(probeRunner).ValidateAsync(executable,finalOptions,token);
                await new FfmpegAnimationProbe(probeRunner).ValidateAsync(executable,finalOptions,token);
            }
            WgcVideoSource? wgcSource=null;string? sourceFallback=null;
            var effectiveBackend=CaptureBackend.Gdi;
            if(!options.UseCustomCommand&&request.CaptureBackend!=CaptureBackend.Gdi&&request.MovableMonitor is {} wgcMonitor&&openMovableWgc is not null)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    video=openMovableWgc(request.Region,wgcMonitor,options,lifetime.Token);
                    effectiveBackend=CaptureBackend.WindowsGraphicsCapture;
                }
                catch(CaptureSourceUnavailableException error)
                {
                    token.ThrowIfCancellationRequested();
                    DiagnosticLog.Write("Movable WGC unavailable; trying fixed WGC: "+error.Message);
                }
            }
            if(video is null&&!options.UseCustomCommand&&request.CaptureBackend!=CaptureBackend.Gdi)
            {
                try
                {
                    var required=capabilities.MissingVideoSource(CaptureBackend.WindowsGraphicsCapture);
                    if(required.Count>0)throw new CaptureSourceUnavailableException(Loc.T("FFmpeg is missing: ")+string.Join(", ",required));
                    token.ThrowIfCancellationRequested();
                    wgcSource=resolveWgc is null?throw new CaptureSourceUnavailableException(Loc.T("The WGC video source is unavailable.")):resolveWgc(request.Region,options);
                    token.ThrowIfCancellationRequested();
                    required=capabilities.MissingVideoSource(CaptureBackend.WindowsGraphicsCapture,composeMonitors:wgcSource.MonitorCount>1||wgcSource.InputGraph.Contains("overlay=",StringComparison.Ordinal));
                    if(required.Count>0)throw new CaptureSourceUnavailableException(Loc.T("FFmpeg is missing: ")+string.Join(", ",required));
                }
                catch(CaptureSourceUnavailableException error)when(request.CaptureBackend==CaptureBackend.Automatic)
                {token.ThrowIfCancellationRequested();wgcSource=null;sourceFallback=error.Message;}
            }
            if(wgcSource is not null)effectiveBackend=CaptureBackend.WindowsGraphicsCapture;
            else if(video is null&&!options.UseCustomCommand&&request.MovableMonitor is {} monitor&&openMovableVideo is not null)
            {
                token.ThrowIfCancellationRequested();
                video=openMovableVideo(request.Region,monitor,options,lifetime.Token);
            }
            if(wgcSource is not null||video is not null)
                preview=FfmpegCommandBuilder.Build(options,request.Region,request.WithAudio,temporary,placeholder,wgcSource,video?.Arguments);
            var missing=Missing(capabilities,preview.Arguments);
            if(missing.Count>0)throw new InvalidOperationException(Loc.T("FFmpeg is missing: ")+string.Join(", ",missing)+Loc.T(". Select a supported encoder/input in Video settings."));
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            IReadOnlyList<string>? input=null;
            if(request.WithAudio)
            {
                if(options.AudioSource==AudioSource.DirectShow)input=["-thread_queue_size","1024","-f","dshow","-i","audio="+options.DirectShowDevice];
                else
                {
                    async Task<IRecordingAudioSource> Open()=>request.LiveSources is {} sources
                        ? await ((ILiveRecordingAudioFactory)audio).OpenLiveAsync(options,sources,lifetime.Token)
                        : await audio.OpenAsync(options,lifetime.Token);
                    var opening=Open();
                    try{source=await opening.WaitAsync(startTimeout??TimeSpan.FromSeconds(12),token);}
                    catch{_=DisposeLateAudioAsync(opening);throw;}
                    input=source.Arguments;
                }
            }
            token.ThrowIfCancellationRequested();
            var command=FfmpegCommandBuilder.Build(options,request.Region,request.WithAudio,temporary,input,wgcSource,video?.Arguments);
            if(startGate is not null)await startGate.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            process=processes.Start(executable,new[]{"-progress","pipe:1","-stats_period","0.2","-nostats","-loglevel","warning"}.Concat(command.Arguments).ToArray());
            run=new RecordingRun(process,source,lifetime,target,temporary,stopTimeout??TimeSpan.FromSeconds(15),command.Width,command.Height,
                effectiveBackend,sourceFallback,video);
            _=run.Completion.ContinueWith(_=>Volatile.Write(ref occupied,0),CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            var wait=Task.WhenAny(process.Ready,process.Exit,source?.Completion??Task.Delay(Timeout.Infinite),video?.Completion??Task.Delay(Timeout.Infinite));
            var ready=await wait.WaitAsync(startTimeout??TimeSpan.FromSeconds(12),token);
            token.ThrowIfCancellationRequested();
            if(ready==video?.Completion)
            {
                await video.Completion;
                throw new IOException(Loc.T("The video source stopped before recording started."));
            }
            if(ready==source?.Completion)
            {
                await source.Completion;
                throw new IOException(Loc.T("The audio source stopped before recording started."));
            }
            if(!process.Ready.IsCompletedSuccessfully)
            {
                if(ready==process.Ready || process.Ready.IsFaulted)await process.Ready;
                throw new IOException(Loc.T("FFmpeg exited before the first video frame. ")+process.Diagnostics);
            }
            token.ThrowIfCancellationRequested();
            run.AcceptStart();return run;
        }
        catch(Exception error)
        {
            if(run is not null)
            {
                run.RejectStart(error.Message);if(error is OperationCanceledException or TimeoutException){try{process!.Kill();}catch(Exception killError){DiagnosticLog.Write("Startup termination failed; continuing cleanup: "+killError.Message);}}var result=await run.StopAsync();
                var recovery=result.PartialPath is null?"":Loc.T(" Recoverable partial: ")+result.PartialPath;
                if(error is OperationCanceledException && token.IsCancellationRequested)throw;
                var message=error.Message.Length>1200?error.Message[..1200]+"…":error.Message;
                var fallback=request.Options.Encoder is (VideoEncoder.H264Nvenc or VideoEncoder.HevcNvenc or VideoEncoder.H264Qsv or VideoEncoder.HevcQsv or VideoEncoder.H264Amf or VideoEncoder.HevcAmf)&&!request.Options.UseCustomCommand?
                    Loc.T(" For a software fallback choose H.264 / x264 in Video settings."):"";
                throw new IOException(Loc.T("Recording could not start: ")+message+fallback+recovery,error);
            }
            lifetime.Cancel();
            try
            {
                try{if(source is not null)await source.DisposeAsync();}
                finally{if(video is not null)await video.DisposeAsync();}
            }
            finally
            {
                try{if(process is not null)await process.DisposeAsync();}
                finally{lifetime.Dispose();Volatile.Write(ref occupied,0);}
            }
            throw;
        }
    }
    private static IReadOnlyList<string> Missing(FfmpegCapabilities capabilities,IReadOnlyList<string> arguments)
    {
        var missing=new HashSet<string>();
        for(var i=0;i+1<arguments.Count;i++)
        {
            var key=arguments[i];var value=arguments[i+1];
            if(key=="-f" && value is "gdigrab" or "dshow" or "lavfi" && !capabilities.Inputs.Contains(value))missing.Add(value+Loc.T(" input"));
            if(key is "-c:v" or "-c:a" && !capabilities.Encoders.Contains(value))missing.Add(value+Loc.T(" encoder"));
            if(key=="-vf")foreach(var part in value.Split(','))
            {
                var name=part.Split('=')[0].Trim();
                if(name.Length>0 && name.All(ch=>char.IsAsciiLetterOrDigit(ch)||ch=='_') && !capabilities.Filters.Contains(name))missing.Add(name+Loc.T(" filter"));
            }
        }
        return missing.ToArray();
    }
    private static async Task DisposeLateAudioAsync(Task<IRecordingAudioSource> opening)
    {
        try{await (await opening.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);}
        catch{ /* A timed-out native setup is already reported; observe and release its eventual result. */ }
    }
}

public sealed class RecordingRun
{
    public CaptureBackend EffectiveCaptureBackend {get;}
    public string? CaptureFallbackReason {get;}
    private readonly IRecordingProcess process;
    private readonly IRecordingAudioSource? audio;
    private readonly IMovableRecordingVideo? video;
    private readonly CancellationTokenSource lifetime;
    private readonly string target,temporary;
    private readonly TimeSpan stopTimeout;
    private readonly TaskCompletionSource<bool> startDecision=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object gate=new();
    private Task<RecordingResult>? stopping;
    private string? failure;
    private int stopSignalled;
    public bool IsStopping=>Volatile.Read(ref stopSignalled)!=0;
    public RecordingProgress Progress=>process.Progress;
    public bool CanToggleMicrophone=>audio is ILiveRecordingAudioSource;
    public bool CanMoveRegion=>video is not null&&!IsStopping&&!process.Exit.IsCompleted;
    internal void MoveRegion(PixelRect region)=>video!.Move(region);
    public async Task SetMicrophoneEnabledAsync(bool enabled,CancellationToken token)
    {
        if(audio is not ILiveRecordingAudioSource live)throw new NotSupportedException(Loc.T("Live microphone control is unavailable for this audio source."));
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);deadline.CancelAfter(TimeSpan.FromSeconds(12));
        await live.SetMicrophoneEnabledAsync(enabled,deadline.Token);
    }
    public int Width {get;}
    public int Height {get;}
    public Task<RecordingResult> Completion {get;}
    internal RecordingRun(IRecordingProcess process,IRecordingAudioSource? audio,CancellationTokenSource lifetime,string target,string temporary,TimeSpan stopTimeout,int width,int height,
        CaptureBackend effectiveCaptureBackend=CaptureBackend.Gdi,string? captureFallbackReason=null,IMovableRecordingVideo? video=null)
    {
        Width=width;Height=height;EffectiveCaptureBackend=effectiveCaptureBackend;CaptureFallbackReason=captureFallbackReason;
        this.process=process;this.audio=audio;this.video=video;this.lifetime=lifetime;this.target=target;this.temporary=temporary;this.stopTimeout=stopTimeout;
        Completion=ObserveAsync();
    }
    internal void AcceptStart()=>startDecision.TrySetResult(true);
    internal void RejectStart(string reason){SetFailure(reason);startDecision.TrySetResult(false);}
    public Task<RecordingResult> StopAsync(){lock(gate)return stopping??=StopAndWaitAsync();}
    private async Task<RecordingResult> StopAndWaitAsync(){await EnsureExitAsync();return await Completion;}
    private async Task EnsureExitAsync()
    {
        try
        {
            if(!process.Exit.IsCompleted && Interlocked.Exchange(ref stopSignalled,1)==0)
                await process.RequestStopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            await process.Exit.WaitAsync(stopTimeout);
        }
        catch(Exception error)
        {
            SetFailure(Loc.T("FFmpeg did not stop cleanly: ")+error.Message);
            try{process.Kill();await process.Exit.WaitAsync(TimeSpan.FromSeconds(3));}
            catch(Exception killError){SetFailure(killError.Message);}
        }
    }
    private async Task<RecordingResult> ObserveAsync()
    {
        var videoObservation=video is null?Task.CompletedTask:ObserveVideoAsync(video);
        var code=-1;
        try
        {
            if(audio is not null)
            {
                var first=await Task.WhenAny(process.Exit,audio.Completion);
                if(first==audio.Completion && !process.Exit.IsCompleted)
                {
                    var transportClosed=AudioError() is RecordingAudioConsumerClosedException;
                    if(transportClosed)
                    {
                        // FFmpeg may close its input before finishing mux/faststart on normal Stop or -t.
                        // Allow that only if it actually exits cleanly within the finalization deadline.
                        try{await process.Exit.WaitAsync(stopTimeout);}
                        catch(Exception error){SetFailure(Loc.T("The audio consumer closed without completing the recording: ")+error.Message);await EnsureExitAsync();}
                    }
                    else
                    {
                        try{await audio.Completion;SetFailure(Loc.T("The audio source stopped unexpectedly."));}
                        catch(Exception error){SetFailure(Loc.T("Audio recording failed: ")+error.Message);}
                        await EnsureExitAsync();
                    }
                }
                if(audio.Completion.IsFaulted && AudioError() is not RecordingAudioConsumerClosedException)
                    SetFailure(Loc.T("Audio recording failed: ")+AudioError()!.Message);
            }
            code=await process.Exit;
            await videoObservation;
            await startDecision.Task;
            if(code!=0)SetFailure(Loc.T("FFmpeg exited with code ")+code+". "+process.Diagnostics);
        }
        catch(Exception error){SetFailure(error.Message);}
        finally
        {
            lifetime.Cancel();
            try{if(audio is not null)await audio.DisposeAsync();}catch(Exception error){SetFailure(Loc.T("Audio cleanup failed: ")+error.Message);}
            try{if(video is not null)await video.DisposeAsync();}catch(Exception error){SetFailure(Loc.T("Video cleanup failed: ")+error.Message);}
            try{await process.DisposeAsync();}catch(Exception error){SetFailure(error.Message);}
            lifetime.Dispose();
        }
        try
        {
            var present=File.Exists(temporary)&&new FileInfo(temporary).Length>0;
            if(failure is null && code==0 && present)
            {
                File.Move(temporary,target,true);return new(true,target,null,null);
            }
            if(!present && File.Exists(temporary))File.Delete(temporary);
            return new(false,null,present?temporary:null,failure??Loc.T("FFmpeg produced no usable output file."));
        }
        catch(Exception error){return new(false,null,File.Exists(temporary)?temporary:null,Loc.T("Could not finalize recording: ")+error.Message);}
    }
    private async Task ObserveVideoAsync(IMovableRecordingVideo source)
    {
        var completed=await Task.WhenAny(process.Exit,source.Completion);
        if(completed!=source.Completion&&!source.Completion.IsFaulted)return;
        try
        {
            await source.Completion;
            await process.Exit.WaitAsync(stopTimeout);
        }
        catch(Exception error)
        {
            SetFailure(Loc.T("Video recording failed: ")+error.Message);
            await EnsureExitAsync();
        }
    }
    private void SetFailure(string value)=>Interlocked.CompareExchange(ref failure,value,null);
    private Exception? AudioError()=>audio?.Completion.Exception?.Flatten().InnerExceptions.FirstOrDefault();
}


