using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;

public sealed class RecordingEngineTests : IDisposable
{
    [Fact] public async Task UnsupportedEncoderHelpStopsBeforeAudioAndProcess()
    {
        var factory=new Factory(new FakeProcess());var audio=new Audio();
        await Assert.ThrowsAsync<ArgumentException>(()=>new RecordingEngine(new Probe{RejectEncoderHelp=true},factory,audio,_=>"fixture")
            .StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"unsupported.mp4"),true)));
        Assert.Null(factory.Arguments);Assert.Equal(0,audio.Opened);
    }
    [Fact]
    public async Task CancelBeforeFirstFrameKillsStartupWithoutWaitingForGracefulStop()
    {
        var process=new FakeProcess{ReadyTask=new TaskCompletionSource().Task};var factory=new Factory(process);using var cancellation=new CancellationTokenSource();
        var starting=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture").StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"early.mp4"),false),cancellation.Token);
        cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>starting.WaitAsync(TimeSpan.FromSeconds(3)));Assert.True(process.Killed);
    }
    [Fact]
    public async Task FailedImmediateStartupKillStillRunsCleanup()
    {
        var process=new FakeProcess{ReadyTask=new TaskCompletionSource().Task,ThrowOnKill=true,CompleteOnStop=true};var factory=new Factory(process);using var cancellation=new CancellationTokenSource();
        var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture");
        var starting=engine.StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"kill-failure.mp4"),false),cancellation.Token);
        cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>starting.WaitAsync(TimeSpan.FromSeconds(3)));Assert.Equal(1,process.Stops);
    }
    [Fact]
    public async Task LiveAudioStartsSilentAndAllowsMicWithoutReplacingTheVideoProcess()
    {
        var process=new FakeProcess();var factory=new Factory(process);var audio=new LiveAudio();
        var engine=new RecordingEngine(new Probe(),factory,audio,_=>"ffmpeg.exe");
        var run=await engine.StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"live.mp4"),true){LiveSources=new(false,false)});
        Assert.Equal(new(false,false),audio.Sources);Assert.True(run.CanToggleMicrophone);
        await run.SetMicrophoneEnabledAsync(true,default);Assert.True(audio.Source.MicrophoneEnabled);
        await File.WriteAllTextAsync(factory.Arguments!.Last(),"video");process.Exited.SetResult(0);Assert.True((await run.Completion).Success);
    }
    private sealed class LiveAudio:ILiveRecordingAudioFactory
    {
        public RecordingSourceChoices? Sources;public LiveSource Source=new();
        public Task<ILiveRecordingAudioSource> OpenLiveAsync(RecordingOptions options,RecordingSourceChoices sources,CancellationToken token){Sources=sources;return Task.FromResult<ILiveRecordingAudioSource>(Source);}
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token)=>throw new InvalidOperationException("Fixed source should not open.");
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>throw new InvalidOperationException();
    }
    private sealed class LiveSource:ILiveRecordingAudioSource
    {
        public bool MicrophoneEnabled{get;private set;}
        public Task SetMicrophoneEnabledAsync(bool value,CancellationToken token){MicrophoneEnabled=value;return Task.CompletedTask;}
        public IReadOnlyList<string> Arguments=>["-f","f32le","-i","fixture"];public Task Completion=>Task.Delay(Timeout.Infinite);
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private readonly string directory=Path.Combine(Path.GetTempPath(),"ShareVibeX-record-tests",Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task SilentRecordingFinalizesOnlyAfterExitAndRepeatedStopIsSafe()
    {
        var process=new FakeProcess();var factory=new Factory(process);var audio=new Audio();
        var engine=new RecordingEngine(new Probe(),factory,audio,_=>"ffmpeg.exe");
        var output=Path.Combine(directory,"clip.mp4");Directory.CreateDirectory(directory);await File.WriteAllTextAsync(output,"old");
        var run=await engine.StartAsync(new(new(),new(0,0,101,99),output,false));
        Assert.Contains("-an",factory.Arguments!);Assert.Equal(0,audio.Opened);
        Assert.Equal("old",await File.ReadAllTextAsync(output));
        await File.WriteAllTextAsync(factory.Arguments!.Last(),"muxed video");
        var first=run.StopAsync();var second=run.StopAsync();
        Assert.False(first.IsCompleted);Assert.Equal("old",await File.ReadAllTextAsync(output));
        process.Exited.SetResult(0);var result=await first;await second;
        Assert.True(result.Success);Assert.Equal("muxed video",await File.ReadAllTextAsync(output));Assert.Equal(1,process.Stops);
    }
    [Fact]
    public async Task FailedProcessPreservesOldOutputAndRecoverablePartial()
    {
        var process=new FakeProcess();var factory=new Factory(process);
        var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"ffmpeg.exe");
        var output=Path.Combine(directory,"clip.mp4");Directory.CreateDirectory(directory);await File.WriteAllTextAsync(output,"old");
        var run=await engine.StartAsync(new(new(),new(0,0,100,100),output,false));
        await File.WriteAllTextAsync(factory.Arguments!.Last(),"partial");process.Exited.SetResult(1);
        var result=await run.Completion;
        Assert.False(result.Success);Assert.True(File.Exists(result.PartialPath));Assert.Equal("old",await File.ReadAllTextAsync(output));
    }
    [Fact]
    public async Task AudioFailureCannotBecomeSilentSuccess()
    {
        var process=new FakeProcess();var factory=new Factory(process);var audio=new Audio();
        var engine=new RecordingEngine(new Probe(),factory,audio,_=>"ffmpeg.exe");
        var run=await engine.StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"audio.mp4"),true));
        Assert.Equal(1,audio.Opened);Assert.Contains("1:a:0",factory.Arguments!);
        audio.Source.Ended.SetException(new IOException("device removed"));
        await Task.Delay(20);process.Exited.SetResult(0);
        var result=await run.Completion;Assert.False(result.Success);Assert.Contains("device removed",result.Error);Assert.True(audio.Source.Disposed);
    }
    [Fact]
    public async Task MissingCapabilityNeverStartsProcessOrAudio()
    {
        var factory=new Factory(new FakeProcess());var audio=new Audio();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new RecordingEngine(new Probe{Missing=true},factory,audio,_=>"ffmpeg.exe")
            .StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"clip.mp4"),true)));
        Assert.Null(factory.Arguments);Assert.Equal(0,audio.Opened);
    }
    [Fact]
    public async Task CompletedStartupDetachesCallerCancellationFromAudioLifetime()
    {
        using var caller=new CancellationTokenSource();var process=new FakeProcess();var audio=new Audio();var factory=new Factory(process);
        var run=await new RecordingEngine(new Probe(),factory,audio,_=>"ffmpeg.exe").StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"audio.mp4"),true),caller.Token);
        caller.Cancel();Assert.False(audio.Token.IsCancellationRequested);
        await File.WriteAllTextAsync(factory.Arguments!.Last(),"video");process.Exited.SetResult(0);
        Assert.True((await run.Completion).Success);
    }
    [Fact]
    public async Task HungStopKillsChildAndDoesNotPublishPartialAsFinal()
    {
        var process=new FakeProcess();var factory=new Factory(process);
        var run=await new RecordingEngine(new Probe(),factory,new Audio(),_=>"ffmpeg.exe",stopTimeout:TimeSpan.FromMilliseconds(30))
            .StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"clip.mp4"),false));
        await File.WriteAllTextAsync(factory.Arguments!.Last(),"partial");
        var result=await run.StopAsync();Assert.False(result.Success);Assert.True(process.Killed);Assert.True(File.Exists(result.PartialPath));
    }
    [Fact]
    public async Task NoFirstFrameTimesOutAndReleasesTheSession()
    {
        var process=new FakeProcess{ReadyTask=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task};var factory=new Factory(process);
        await Assert.ThrowsAsync<IOException>(()=>new RecordingEngine(new Probe(),factory,new Audio(),_=>"ffmpeg.exe",TimeSpan.FromMilliseconds(30),TimeSpan.FromMilliseconds(30))
            .StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"clip.mp4"),false)));
        Assert.True(process.Killed);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AudioConsumerClosingDuringCleanStopOrDurationDoesNotRejectValidVideo(bool manualStop)
    {
        var process=new FakeProcess();var factory=new Factory(process);var audio=new Audio();
        var run=await new RecordingEngine(new Probe(),factory,audio,_=>"ffmpeg.exe")
            .StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"clip.mp4"),true));
        await File.WriteAllTextAsync(factory.Arguments!.Last(),"muxed video");
        var stopping=manualStop?run.StopAsync():null;
        audio.Source.Ended.SetException(new RecordingAudioConsumerClosedException("FFmpeg closed the pipe."));
        await Task.Delay(20);process.Exited.SetResult(0);
        var result=await (stopping??run.Completion);Assert.True(result.Success,result.Error);Assert.Null(result.PartialPath);
    }
    [Fact]
    public async Task MissingDirectShowInputIsRejectedBeforeStartingFfmpeg()
    {
        var factory=new Factory(new FakeProcess());var audio=new Audio();
        var engine=new RecordingEngine(new Probe{NoDirectShow=true},factory,audio,_=>"ffmpeg.exe");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>engine.StartAsync(new(new(){AudioSource=AudioSource.DirectShow,DirectShowDevice="Example audio"},
            new(0,0,100,100),Path.Combine(directory,"clip.mp4"),true)));
        Assert.Null(factory.Arguments);Assert.Equal(0,audio.Opened);
    }
    [Fact]
    public async Task TimedOutAudioSetupDoesNotStartFfmpegAndDisposesLateSource()
    {
        var factory=new Factory(new FakeProcess());var audio=new Audio{Pending=new(TaskCreationOptions.RunContinuationsAsynchronously)};
        var engine=new RecordingEngine(new Probe(),factory,audio,_=>"ffmpeg.exe",startTimeout:TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAsync<TimeoutException>(()=>engine.StartAsync(new(new(),new(0,0,100,100),Path.Combine(directory,"clip.mp4"),true)));
        Assert.Null(factory.Arguments);Assert.True(audio.Token.IsCancellationRequested);
        audio.Pending.SetResult(audio.Source);await audio.Source.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    internal sealed class FakeProcess:IRecordingProcess
    {
        public TaskCompletionSource<int> Exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ReadyTask=Task.CompletedTask;public Task Ready=>ReadyTask;public Task<int> Exit=>Exited.Task;public string Diagnostics=>"encoder failed";public int Stops;public bool Killed;
        public bool ThrowOnKill,CompleteOnStop;
        public Task RequestStopAsync(){Stops++;if(CompleteOnStop)Exited.TrySetResult(0);return Task.CompletedTask;}public void Kill(){if(ThrowOnKill)throw new System.ComponentModel.Win32Exception("kill denied");Killed=true;Exited.TrySetResult(-1);}public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Factory(FakeProcess process):IRecordingProcessFactory
    { public IReadOnlyList<string>? Arguments;public IRecordingProcess Start(string executable,IReadOnlyList<string> arguments){Arguments=arguments;return process;} }
    private sealed class Probe:ICommandRunner
    {
        public bool Missing,NoDirectShow,RejectEncoderHelp;
        public Task<ToolResult> RunAsync(string exe,IReadOnlyList<string> args,CancellationToken token)=>Task.FromResult(new ToolResult(0,args.Last() switch
        {"-version"=>"ffmpeg version test\n","-devices"=>Missing?"":NoDirectShow?" D  gdigrab Desktop\n":" D  gdigrab Desktop\n D  dshow DirectShow\n",
            "-encoders"=>" V....D libx264 H264\n A....D aac AAC\n","encoder=libx264"=>RejectEncoderHelp?"Encoder libx264 [fixture]:\n":"Encoder libx264 [H264]:\n -preset <string> E..V....... preset\n -tune <string> E..V....... tune\n -crf <float> E..V....... quality (from -1 to 51)\n",
            "-filters"=>" ... scale V->V\n ... pad V->V\n ... format V->V\n",_=>""},""));
    }
    private sealed class Audio:IRecordingAudioFactory
    {
        public int Opened;public AudioSourceFake Source=new();public CancellationToken Token;public TaskCompletionSource<IRecordingAudioSource>? Pending;
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token){Opened++;Token=token;return Pending?.Task??Task.FromResult<IRecordingAudioSource>(Source);}
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<RecordingAudioDevice>>([]);
    }
    private sealed class AudioSourceFake:IRecordingAudioSource
    {
        public IReadOnlyList<string> Arguments=>["-f","f32le","-ar","48000","-ac","2","-i",@"\\.\pipe\test-audio"];
        public TaskCompletionSource Ended=new(TaskCreationOptions.RunContinuationsAsynchronously);public Task Completion=>Ended.Task;public bool Disposed;
        public TaskCompletionSource DisposedSignal=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask DisposeAsync(){Disposed=true;Ended.TrySetResult();DisposedSignal.TrySetResult();return ValueTask.CompletedTask;}
    }
}
