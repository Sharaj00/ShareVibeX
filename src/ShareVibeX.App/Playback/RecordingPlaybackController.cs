using System.Diagnostics;
using ShareVibeX.Core.Playback;

namespace ShareVibeX.App.Playback;

public enum PreviewPlaybackState { Empty, Loading, Paused, Playing, Seeking, Ended, Failed, Closed }

public sealed class RecordingPlaybackController(IPreviewDecoderFactory decoders,IPreviewAudioOutputFactory audioOutputs,
    Func<PreviewFrame,long,Task> present):IAsyncDisposable
{
    private readonly object gate=new();private readonly SemaphoreSlim commands=new(1);
    private readonly Queue<CancellationTokenSource> retired=[];
    private CancellationTokenSource? current;
    private Task running=Task.CompletedTask;private Task? closing;
    private long generation;private bool closed,wantsPlayback;
    private PreviewMedia? media;private PreviewFormat format;
    private TimeSpan position;private Func<TimeSpan>? liveClock;
    public PreviewPlaybackState State {get;private set;}
    public string? Error {get;private set;}
    public long Generation=>Interlocked.Read(ref generation);
    public TimeSpan Position
    {
        get{try{return Clamp(State==PreviewPlaybackState.Playing&&liveClock is {} clock?clock():position);}catch{return Clamp(position);}}
    }
    public TimeSpan? Duration=>media?.Duration;
    public Task PlaybackCompletion=>running;
    public event Action? Changed;
    private sealed record Request(long Generation,CancellationToken Token);
    private Request Begin(bool allowClose=false,CancellationToken token=default)
    {
        lock(gate)
        {
            if(closed&&!allowClose)throw new ObjectDisposedException(nameof(RecordingPlaybackController));
            if(current is not null){current.Cancel();retired.Enqueue(current);}
            current=CancellationTokenSource.CreateLinkedTokenSource(token);return new(++generation,current.Token);
        }
    }
    private bool IsCurrent(Request request)=>request.Generation==Generation&&!request.Token.IsCancellationRequested;
    private async Task ExecuteAsync(Request request,Func<Request,Task> action)
    {
        await commands.WaitAsync().ConfigureAwait(false);
        try
        {
            try{await running.ConfigureAwait(false);}catch{ /* Prior run failures were reported before resource release. */ }
            lock(gate)while(retired.Count>0)retired.Dequeue().Dispose();
            if(!IsCurrent(request))return;Error=null;await action(request).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(!IsCurrent(request)){}
        catch(Exception error){if(IsCurrent(request)){Error=error.Message;State=PreviewPlaybackState.Failed;Changed?.Invoke();}throw;}
        finally{commands.Release();}
    }
    public async Task LoadAsync(PreviewMedia value,PreviewFormat target,CancellationToken token=default)
    {
        var request=Begin(token:token);wantsPlayback=false;
        await ExecuteAsync(request,async r=>
        {
            if(value.Duration<TimeSpan.Zero)throw new ArgumentException(Loc.T("Recording duration cannot be negative."));
            media=value;format=target;position=TimeSpan.Zero;State=PreviewPlaybackState.Loading;Changed?.Invoke();await ThumbnailAsync(r).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if(token.IsCancellationRequested)
        {
            if(request.Generation==Generation){State=PreviewPlaybackState.Paused;Changed?.Invoke();}
            token.ThrowIfCancellationRequested();
        }
    }
    public Task PlayAsync()
    {
        if(State==PreviewPlaybackState.Playing)return Task.CompletedTask;
        var start=State==PreviewPlaybackState.Ended?TimeSpan.Zero:Position;var request=Begin();wantsPlayback=true;
        return ExecuteAsync(request,r=>StartPlayingAsync(r,start));
    }
    public Task PauseAsync()
    {
        var paused=Position;var request=Begin();wantsPlayback=false;
        return ExecuteAsync(request,_=>{position=paused;State=PreviewPlaybackState.Paused;Changed?.Invoke();return Task.CompletedTask;});
    }
    public Task SeekAsync(TimeSpan target)
    {
        var request=Begin();
        return ExecuteAsync(request,async r=>
        {
            position=ClampSeek(target);State=PreviewPlaybackState.Seeking;Changed?.Invoke();
            if(wantsPlayback)await StartPlayingAsync(r,position).ConfigureAwait(false);else await ThumbnailAsync(r).ConfigureAwait(false);
        });
    }
    public async Task StepFrameAsync(int direction)
    {
        if(direction is not(-1 or 1))throw new ArgumentOutOfRangeException(nameof(direction));
        if(media?.Duration is not {} duration)throw new InvalidOperationException(Loc.T("Frame stepping requires known duration."));
        await PauseAsync().ConfigureAwait(false);var rate=media.FrameRate;
        var frame=(long)Math.Round(Position.TotalSeconds*rate,MidpointRounding.AwayFromZero)+direction;
        var last=Math.Max(0,(long)Math.Round(duration.TotalSeconds*rate)-1);
        await SeekAsync(TimeSpan.FromSeconds(Math.Clamp(frame,0,last)/(double)rate)).ConfigureAwait(false);
    }
    private async Task<IPreviewDecoder> OpenDecoderAsync(Request request,bool audio)
    {
        if(media is null)throw new InvalidOperationException(Loc.T("No recording is loaded."));
        var opening=decoders.OpenAsync(media,format,position,audio,request.Token);
        try{var decoder=await opening.WaitAsync(request.Token).ConfigureAwait(false);request.Token.ThrowIfCancellationRequested();return decoder;}
        catch{await DisposeLateAsync(opening).ConfigureAwait(false);throw;}
    }
    private static async Task DisposeLateAsync<T>(Task<T> opening)where T:IAsyncDisposable
    {try{await(await opening.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);}catch{}}
    private async Task ThumbnailAsync(Request request)
    {
        await using var decoder=await OpenDecoderAsync(request,false).ConfigureAwait(false);
        using var frame=await PreviewFrameReader.ReadAsync(decoder.Video,format,request.Token).ConfigureAwait(false);
        if(frame is null){await decoder.VideoCompletion.WaitAsync(request.Token).ConfigureAwait(false);throw new InvalidDataException(Loc.T("No video frame is available at this position."));}
        if(IsCurrent(request))
        {
            await present(frame,request.Generation).ConfigureAwait(false);
            if(IsCurrent(request)){State=PreviewPlaybackState.Paused;Changed?.Invoke();}
        }
    }
    private async Task StartPlayingAsync(Request request,TimeSpan start)
    {
        position=start;var decoder=await OpenDecoderAsync(request,media?.HasAudio==true).ConfigureAwait(false);IPreviewAudioOutput? audio=null;
        try
        {
            if(media!.HasAudio)
            {
                if(decoder.Audio is null)throw new InvalidDataException(Loc.T("The recording audio stream is unavailable."));
                var opening=audioOutputs.OpenAsync(request.Token);
                try{audio=await opening.WaitAsync(request.Token).ConfigureAwait(false);request.Token.ThrowIfCancellationRequested();}
                catch{await DisposeLateAsync(opening).ConfigureAwait(false);audio=null;throw;}
            }
            request.Token.ThrowIfCancellationRequested();State=PreviewPlaybackState.Playing;Changed?.Invoke();
            running=PlayRunAsync(request,decoder,audio,start);
        }
        catch{try{if(audio is not null)await audio.DisposeAsync().ConfigureAwait(false);}finally{await decoder.DisposeAsync().ConfigureAwait(false);}throw;}
    }
    private async Task PlayRunAsync(Request request,IPreviewDecoder decoder,IPreviewAudioOutput? audio,TimeSpan start)
    {
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(request.Token);var token=lifetime.Token;
        var watch=new Stopwatch();var tail=new Stopwatch();var audioEnd=TimeSpan.Zero;long index=0;
        var pump=audio is null?Task.CompletedTask:PumpAudioAsync(decoder,audio,token);Exception? failure=null;var ended=false;
        async Task ObserveFailureAsync(Task task)
        {
            try{await task.WaitAsync(token).ConfigureAwait(false);}
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
            catch(Exception error){failure??=error;lifetime.Cancel();}
        }
        var outputFailure=audio is null?Task.CompletedTask:ObserveFailureAsync(audio.Failure);
        var pumpFailure=ObserveFailureAsync(pump);
        TimeSpan Clock()
        {
            if(audio is null)return start+watch.Elapsed;
            if(audio.Failure.IsFaulted)audio.Failure.GetAwaiter().GetResult();
            if(pump.IsFaulted)pump.GetAwaiter().GetResult();
            if(pump.IsCompletedSuccessfully){if(!tail.IsRunning){audioEnd=audio.Position;tail.Start();}return start+audioEnd+tail.Elapsed;}
            return start+audio.Position;
        }
        liveClock=Clock;
        try
        {
            while(true)
            {
                using var frame=await PreviewFrameReader.ReadAsync(decoder.Video,format,token).ConfigureAwait(false);
                if(frame is null)break;if(!watch.IsRunning)watch.Start();
                var stamp=start+TimeSpan.FromSeconds(index++/(double)format.FrameRate);
                if(stamp+TimeSpan.FromSeconds(2d/format.FrameRate)<Clock())continue;
                while(stamp>Clock()){await Task.Delay(5,token).ConfigureAwait(false);}
                if(IsCurrent(request)){position=stamp;await present(frame,request.Generation).ConfigureAwait(false);}
            }
            await decoder.VideoCompletion.WaitAsync(token).ConfigureAwait(false);await pump.WaitAsync(token).ConfigureAwait(false);
            if(audio is null && index>0)
            {
                var finish=start+TimeSpan.FromSeconds(index/(double)format.FrameRate);
                while(Clock()<finish)await Task.Delay(5,token).ConfigureAwait(false);
            }
            ended=true;
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception error){failure=error;}
        finally
        {
            lifetime.Cancel();await Task.WhenAll(outputFailure,pumpFailure).ConfigureAwait(false);
            try{await pump.ConfigureAwait(false);}catch(Exception error){if(error is not OperationCanceledException)failure??=error;}
            try{if(audio is not null)await audio.DisposeAsync().ConfigureAwait(false);}catch(Exception error){failure??=error;}
            try{await decoder.DisposeAsync().ConfigureAwait(false);}catch(Exception error){failure??=error;}
            liveClock=null;
            if(IsCurrent(request))
            {
                if(failure is not null){Error=failure.Message;State=PreviewPlaybackState.Failed;}
                else if(ended){position=Clamp(start+TimeSpan.FromSeconds(index/(double)format.FrameRate));State=PreviewPlaybackState.Ended;}
                Changed?.Invoke();
            }
        }
    }
    private static async Task PumpAudioAsync(IPreviewDecoder decoder,IPreviewAudioOutput output,CancellationToken token)
    {
        output.Start();var bytes=new byte[8200];var carry=0;
        while(true)
        {
            var read=await decoder.Audio!.ReadAsync(bytes.AsMemory(carry,8192),token).ConfigureAwait(false);if(read==0)break;
            var count=carry+read;var aligned=count-count%8;if(aligned>0)await output.WriteAsync(bytes.AsMemory(0,aligned),token).ConfigureAwait(false);
            carry=count-aligned;if(carry>0)Buffer.BlockCopy(bytes,aligned,bytes,0,carry);
        }
        if(carry!=0)throw new InvalidDataException(Loc.T("The audio decoder returned an incomplete stereo sample."));
        await decoder.AudioCompletion.WaitAsync(token).ConfigureAwait(false);await output.DrainAsync(token).ConfigureAwait(false);
    }
    private TimeSpan Clamp(TimeSpan value)=>value<TimeSpan.Zero?TimeSpan.Zero:media?.Duration is {} duration&&value>duration?duration:value;
    private TimeSpan ClampSeek(TimeSpan value)
    {
        value=Clamp(value);if(media?.Duration is {} duration && value>=duration)
            return TimeSpan.FromTicks(Math.Max(0,duration.Ticks-TimeSpan.TicksPerSecond/Math.Max(1,media.FrameRate)));
        return value;
    }
    public ValueTask DisposeAsync()
    {
        lock(gate)
        {
            if(closing is not null)return new(closing);
            var request=Begin(true);closed=true;wantsPlayback=false;
            closing=ExecuteAsync(request,_=>{State=PreviewPlaybackState.Closed;Changed?.Invoke();return Task.CompletedTask;});return new(closing);
        }
    }
}
