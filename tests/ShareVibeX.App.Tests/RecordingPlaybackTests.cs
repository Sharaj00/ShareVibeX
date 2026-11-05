using ShareVibeX.Core.Playback;
using ShareVibeX.App.Playback;

namespace ShareVibeX.App.Tests;

public sealed class RecordingPlaybackTests
{
    private static readonly PreviewFormat Format=new(1,1,30);
    private static PreviewMedia Media(bool audio=false)=>new("fixture.mp4",null,TimeSpan.FromSeconds(1),1,1,30,audio);
    [Fact]
    public async Task FirstFrameIsPausedAndSeekingDoesNotOpenAudio()
    {
        var factory=new Factory();var output=new AudioFactory();var pixels=new List<byte>();
        await using var playback=new RecordingPlaybackController(factory,output,(frame,_)=>{pixels.Add(frame.Pixels.Span[0]);return Task.CompletedTask;});
        await playback.LoadAsync(Media(true),Format);Assert.Equal(PreviewPlaybackState.Paused,playback.State);Assert.Equal(0,output.Opened);
        await playback.SeekAsync(TimeSpan.FromSeconds(.5));Assert.Equal(TimeSpan.FromSeconds(.5),playback.Position);
        Assert.Equal(2,pixels.Count);Assert.All(factory.Decoders,d=>Assert.True(d.Disposed));Assert.All(factory.AudioRequests,a=>Assert.False(a));
    }
    [Fact]
    public async Task SupersedingSeekDisposesLateDecoderAndRejectsItsFrames()
    {
        var factory=new Factory();var pixels=new List<byte>();
        await using var playback=new RecordingPlaybackController(factory,new AudioFactory(),(frame,_)=>{pixels.Add(frame.Pixels.Span[0]);return Task.CompletedTask;});
        await playback.LoadAsync(Media(),Format);var late=new TaskCompletionSource<IPreviewDecoder>(TaskCreationOptions.RunContinuationsAsynchronously);factory.Pending=late.Task;
        var old=playback.SeekAsync(TimeSpan.FromSeconds(.2));await factory.PendingOpened.Task;
        factory.Pending=null;var latest=playback.SeekAsync(TimeSpan.FromSeconds(.8));var stale=new Decoder(99);
        try{await Task.WhenAny(latest,Task.Delay(100));Assert.False(latest.IsCompleted,"Replacement must wait for the canceled setup to drain.");}
        finally{late.TrySetResult(stale);await Task.WhenAll(old,latest);}
        await stale.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain((byte)99,pixels);Assert.Equal(TimeSpan.FromSeconds(.8),playback.Position);
    }
    [Fact]
    public async Task CloseWaitsForCanceledNativeSetupToBeDisposedBeforeReleasingOwnership()
    {
        var factory=new Factory();var playback=new RecordingPlaybackController(factory,new AudioFactory(),(_,_)=>Task.CompletedTask);
        var pending=new TaskCompletionSource<IPreviewDecoder>(TaskCreationOptions.RunContinuationsAsynchronously);factory.Pending=pending.Task;
        var loading=playback.LoadAsync(Media(),Format);await factory.PendingOpened.Task;
        var closing=playback.DisposeAsync().AsTask();
        try{await Task.WhenAny(closing,Task.Delay(100));Assert.False(closing.IsCompleted,"A preview must retain ownership while setup can still create resources.");}
        finally{pending.TrySetResult(new Decoder(10));await loading;await closing;}
        Assert.Equal(PreviewPlaybackState.Closed,playback.State);
    }
    [Fact]
    public async Task CloseCancelsDecoderCompletionWaitAfterRawStreamEof()
    {
        var factory=new Factory();var playback=new RecordingPlaybackController(factory,new AudioFactory(),(_,_)=>Task.CompletedTask);
        await playback.LoadAsync(Media(),Format);factory.HoldCompletion=true;await playback.PlayAsync();
        await factory.Decoders.Last().CompletionRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var close=playback.DisposeAsync().AsTask();
        try{await close.WaitAsync(TimeSpan.FromSeconds(2));}
        finally{factory.Decoders.Last().PendingCompletion.TrySetResult();await close;}
        Assert.True(factory.Decoders.Last().Disposed);Assert.Equal(PreviewPlaybackState.Closed,playback.State);
    }
    [Fact]
    public async Task CloseCancelsPendingAudioDecoderCompletion()
    {
        var factory=new Factory();var playback=new RecordingPlaybackController(factory,new AudioFactory(),(_,_)=>Task.CompletedTask);
        await playback.LoadAsync(Media(true),Format);factory.HoldAudioCompletion=true;await playback.PlayAsync();
        await factory.Decoders.Last().AudioCompletionRead.Task.WaitAsync(TimeSpan.FromSeconds(2));var close=playback.DisposeAsync().AsTask();
        try{await close.WaitAsync(TimeSpan.FromSeconds(2));}
        finally{factory.Decoders.Last().AudioPendingCompletion.TrySetResult();await close;}
        Assert.True(factory.Decoders.Last().Disposed);
    }
    [Fact]
    public async Task SilentPlaybackEndsAndCanReplayWithoutAudioHardware()
    {
        var factory=new Factory();var audio=new AudioFactory();
        await using var playback=new RecordingPlaybackController(factory,audio,(_,_)=>Task.CompletedTask);
        await playback.LoadAsync(Media(),Format);await playback.PlayAsync();await playback.PlaybackCompletion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(PreviewPlaybackState.Ended,playback.State);Assert.Equal(0,audio.Opened);
        await playback.PlayAsync();await playback.PauseAsync();Assert.Equal(PreviewPlaybackState.Paused,playback.State);
    }
    [Fact]
    public async Task DecoderFailureIsVisibleAndCloseReleasesAllResources()
    {
        var factory=new Factory();var playback=new RecordingPlaybackController(factory,new AudioFactory(),(_,_)=>Task.CompletedTask);
        await playback.LoadAsync(Media(),Format);factory.Fail=true;await playback.PlayAsync();await playback.PlaybackCompletion;
        Assert.Equal(PreviewPlaybackState.Failed,playback.State);Assert.Contains("decode failed",playback.Error);
        await playback.DisposeAsync();Assert.Equal(PreviewPlaybackState.Closed,playback.State);Assert.All(factory.Decoders,d=>Assert.True(d.Disposed));
    }
    [Fact]
    public async Task AudiblePlaybackUsesOutputAndReleasesItOnPause()
    {
        var factory=new Factory();var audio=new AudioFactory();
        await using var playback=new RecordingPlaybackController(factory,audio,(_,_)=>Task.CompletedTask);
        await playback.LoadAsync(Media(true),Format);await playback.PlayAsync();await playback.PlaybackCompletion;
        Assert.Equal(1,audio.Opened);Assert.True(audio.Output.Disposed);Assert.True(audio.Output.Written>0);
    }
    [Fact]
    public async Task AudioFailureCancelsAStalledVideoReadAndPositionRemainsSafeForUi()
    {
        var factory=new Factory();var audio=new AudioFactory();
        await using var playback=new RecordingPlaybackController(factory,audio,(_,_)=>Task.CompletedTask);
        await playback.LoadAsync(Media(true),Format);factory.BlockVideo=true;await playback.PlayAsync();
        audio.Output.ThrowPosition=true;Assert.Null(Record.Exception(()=>{_ = playback.Position;}));
        audio.Output.Failed.TrySetException(new IOException("device gone"));
        await playback.PlaybackCompletion.WaitAsync(TimeSpan.FromSeconds(2));Assert.Equal(PreviewPlaybackState.Failed,playback.State);Assert.Contains("device gone",playback.Error);
    }
    private sealed class Factory:IPreviewDecoderFactory
    {
        public Task<IPreviewDecoder>? Pending;public bool Fail,BlockVideo,HoldCompletion,HoldAudioCompletion;public List<Decoder> Decoders=[];public List<bool> AudioRequests=[];
        public TaskCompletionSource PendingOpened=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IPreviewDecoder> OpenAsync(PreviewMedia media,PreviewFormat format,TimeSpan position,bool audio,CancellationToken token)
        {
            AudioRequests.Add(audio);if(Pending is not null){PendingOpened.TrySetResult();return Pending;}
            var decoder=new Decoder((byte)(position.TotalSeconds*100),audio,Fail,BlockVideo){HoldCompletion=HoldCompletion,HoldAudioCompletion=HoldAudioCompletion};Decoders.Add(decoder);return Task.FromResult<IPreviewDecoder>(decoder);
        }
    }
    private sealed class Decoder(byte value,bool audio=false,bool fail=false,bool block=false):IPreviewDecoder
    {
        public Stream Video {get;}=block?new BlockingStream():new MemoryStream(new byte[]{value,0,0,255,value,0,0,255});
        public Stream? Audio {get;}=audio?new MemoryStream(new byte[3840]):null;
        public bool HoldCompletion;public TaskCompletionSource CompletionRead=new(TaskCreationOptions.RunContinuationsAsynchronously),PendingCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task VideoCompletion{get{CompletionRead.TrySetResult();return HoldCompletion?PendingCompletion.Task:fail?Task.FromException(new IOException("decode failed")):Task.CompletedTask;}}
        public bool HoldAudioCompletion;public TaskCompletionSource AudioCompletionRead=new(TaskCreationOptions.RunContinuationsAsynchronously),AudioPendingCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task AudioCompletion{get{AudioCompletionRead.TrySetResult();return HoldAudioCompletion?AudioPendingCompletion.Task:Task.CompletedTask;}}public bool Disposed;
        public TaskCompletionSource DisposedSignal=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask DisposeAsync(){Disposed=true;Video.Dispose();Audio?.Dispose();DisposedSignal.TrySetResult();return ValueTask.CompletedTask;}
    }
    private sealed class BlockingStream:MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default){await Task.Delay(Timeout.Infinite,token);return 0;}
    }
    private sealed class AudioFactory:IPreviewAudioOutputFactory
    {
        public int Opened;public Output Output=new();
        public Task<IPreviewAudioOutput> OpenAsync(CancellationToken token){Opened++;return Task.FromResult<IPreviewAudioOutput>(Output);}
    }
    private sealed class Output:IPreviewAudioOutput
    {
        public bool ThrowPosition;public TaskCompletionSource Failed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TimeSpan Position=>ThrowPosition?throw new IOException("clock unavailable"):TimeSpan.FromSeconds(1);public Task Failure=>Failed.Task;
        public int Written;public bool Disposed;
        public Task WriteAsync(ReadOnlyMemory<byte> pcm,CancellationToken token){Written+=pcm.Length;return Task.CompletedTask;}
        public void Start(){}public Task DrainAsync(CancellationToken token)=>Task.CompletedTask;
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
}
