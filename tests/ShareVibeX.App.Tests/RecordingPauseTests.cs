using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;

public sealed class RecordingPauseTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"ShareVibeX-pause",Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task MovingTheRegionKeepsOneEncoderAndOneSegment()
    {
        var video=new MovableVideo();
        var fixture=new Fixture(root,video);var session=fixture.Session;
        var start=new ShareVibeX.Core.Capture.PixelRect(10,20,100,100);
        var target=start with{X=70,Y=90};
        await session.StartAsync(new(),start,new(false,false),movableMonitor:new("screen",new(0,0,800,600),1));
        await fixture.WriteAsync();
        Assert.True(session.TryMoveRegion(target));
        Assert.Equal(target,session.Region);
        Assert.Equal(target,video.Region);
        Assert.Equal(RecordingSessionState.Recording,session.State);
        Assert.Empty(session.Segments);
        var args=Assert.Single(fixture.Factory.Arguments);
        Assert.Contains("rawvideo",args);Assert.DoesNotContain("gdigrab",args);
        await session.StopAsync();
        Assert.Single(fixture.Assembler.Segments!);
        Assert.True(session.HasPendingResult);
        Assert.True(video.Disposed);
    }
    private sealed class MovableVideo:IMovableRecordingVideo
    {
        public IReadOnlyList<string> Arguments=>["-f","rawvideo","-i","fixture"];
        public Task Completion=>Task.Delay(Timeout.Infinite);
        public ShareVibeX.Core.Capture.PixelRect Region {get;private set;}
        public bool Disposed {get;private set;}
        public void Move(ShareVibeX.Core.Capture.PixelRect value)=>Region=value;
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
        [Fact]
    public async Task RepeatedPauseResumeStopsWhilePausedAndCompletesSessionOnce()
    {
        var fixture=new Fixture(root);var session=fixture.Session;var finished=0;session.Finished+=_=>finished++;
        await session.StartAsync(new(),new(0,0,100,100),new(false,false));await fixture.WriteAsync();
        await session.PauseAsync();Assert.Equal(RecordingSessionState.Paused,session.State);Assert.True(session.IsActive);
        Assert.Equal(TimeSpan.FromSeconds(2),session.Progress.Duration);Assert.False(session.Completion!.IsCompleted);
        await session.PauseAsync();await session.ResumeAsync();await fixture.WriteAsync();await session.PauseAsync();
        Assert.Equal(TimeSpan.FromSeconds(4),session.Progress.Duration);
        await session.StopAsync();Assert.Equal(2,fixture.Factory.Runs.Count);Assert.Equal(2,fixture.Assembler.Segments!.Count);
        Assert.False(fixture.Assembler.KeepAudio);Assert.Equal(1,finished);Assert.True(session.HasPendingResult);
    }
    [Fact]
    public async Task ResumeFailureRetainsPreviousSegmentsAndAllowsRetry()
    {
        var fixture=new Fixture(root);var session=fixture.Session;
        await session.StartAsync(new(),new(0,0,100,100),new(true,false));await fixture.WriteAsync();await session.PauseAsync();
        fixture.Factory.FailNext=true;await Assert.ThrowsAsync<IOException>(()=>session.ResumeAsync());
        Assert.Equal(RecordingSessionState.Paused,session.State);Assert.True(File.Exists(session.Segments.Single().Path));
        await session.ResumeAsync();await fixture.WriteAsync();await session.StopAsync();Assert.Equal(2,fixture.Assembler.Segments!.Count);
    }
    [Fact]
    public async Task FailedMicEnableRollsBackAndPausedChangeDoesNotOpenDevices()
    {
        var fixture=new Fixture(root);var session=fixture.Session;
        await session.StartAsync(new(),new(0,0,100,100),new(false,false));await fixture.WriteAsync();
        fixture.Audio.Latest!.FailEnable=true;
        await Assert.ThrowsAsync<IOException>(()=>session.SetMicrophoneEnabledAsync(true));Assert.False(session.Sources.Microphone);
        await session.PauseAsync();Assert.True(fixture.Audio.Latest.Disposed);var opened=fixture.Audio.Opened;
        await session.SetMicrophoneEnabledAsync(true);Assert.Equal(opened,fixture.Audio.Opened);
        await session.ResumeAsync();Assert.True(fixture.Audio.Latest!.MicrophoneEnabled);await fixture.WriteAsync();await session.StopAsync();
        Assert.True(fixture.Assembler.KeepAudio);
    }
    [Fact]
    public async Task DurationLimitUsesRecordedTimeRemainingAcrossResume()
    {
        var fixture=new Fixture(root);var session=fixture.Session;
        await session.StartAsync(new(){DurationSeconds=5},new(0,0,100,100),new(false,false));await fixture.WriteAsync();await session.PauseAsync();
        await session.ResumeAsync();var args=fixture.Factory.Arguments.Last();Assert.Equal("3",args[args.IndexOf("-t")+1]);
        await fixture.WriteAsync();await session.StopAsync();
    }
    [Fact]
    public async Task CancelInterruptsAssemblyAndDeletesOnlyOwnedFiles()
    {
        var fixture=new Fixture(root);fixture.Assembler.Hold=true;var session=fixture.Session;
        await session.StartAsync(new(),new(0,0,100,100),new(false,false));await fixture.WriteAsync();
        var stopping=session.StopAsync();await fixture.Assembler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await session.CancelAsync();await stopping;Assert.Equal(RecordingSessionState.Idle,session.State);Assert.False(Directory.Exists(session.Workspace!.DirectoryPath));
    }
    [Fact]
    public async Task StopCancelsResumeStartupAndStillRetainsEarlierMedia()
    {
        var fixture=new Fixture(root);var session=fixture.Session;
        await session.StartAsync(new(),new(0,0,100,100),new(false,false));await fixture.WriteAsync();await session.PauseAsync();
        fixture.Factory.WaitNext=true;var resuming=session.ResumeAsync();await fixture.Factory.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stop=session.StopAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>resuming);await stop;
        Assert.True(session.HasPendingResult);Assert.Single(fixture.Assembler.Segments!);
    }
    private sealed class Fixture
    {
        public Factory Factory=new();public Audio Audio=new();public Assembler Assembler=new();public RecordingSessionController Session;
        public Fixture(string root,MovableVideo? video=null)=>Session=new(new(new Probe(),Factory,Audio,_=>"fixture",stopTimeout:TimeSpan.FromMilliseconds(100),openMovableVideo:video is null?null:(_,_,_,_)=>video),root,Assembler);
        public Task WriteAsync()=>File.WriteAllTextAsync(Factory.Arguments.Last().Last(),"clip");
    }
    private sealed class Probe:ICommandRunner
    {
        public Task<ToolResult> RunAsync(string exe,IReadOnlyList<string> args,CancellationToken token)=>Task.FromResult(new ToolResult(0,args.Last() switch{
            "-version"=>"ffmpeg version fixture","-devices"=>" D  gdigrab input","-encoders"=>" V....D libx264 video\n A....D aac audio","encoder=libx264"=>"Encoder libx264 [H264]:\n -preset <string> E..V....... preset\n -tune <string> E..V....... tune\n -crf <float> E..V....... quality (from -1 to 51)\n",
            "-filters"=>" .. pad V->V\n .. format V->V",_=>""},""));
    }
    private sealed class Factory:IRecordingProcessFactory
    {
        public List<Process> Runs=[];public List<List<string>> Arguments=[];public bool FailNext,WaitNext;
        public TaskCompletionSource Waiting=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IRecordingProcess Start(string exe,IReadOnlyList<string> args)
        {
            if(FailNext){FailNext=false;throw new IOException("encoder start failed");}
            var run=new Process();if(WaitNext){run.Readiness=new(TaskCreationOptions.RunContinuationsAsynchronously);Waiting.TrySetResult();WaitNext=false;}
            Runs.Add(run);Arguments.Add(args.ToList());return run;
        }
    }
    private sealed class Process:IRecordingProcess
    {
        public TaskCompletionSource? Readiness;public Task Ready=>Readiness?.Task??Task.CompletedTask;
        private readonly TaskCompletionSource<int> ended=new(TaskCreationOptions.RunContinuationsAsynchronously);public Task<int> Exit=>ended.Task;
        public RecordingProgress Progress=>new(TimeSpan.FromSeconds(2),4,60);public string Diagnostics=>"";
        public Task RequestStopAsync(){ended.TrySetResult(0);return Task.CompletedTask;}public void Kill()=>ended.TrySetResult(-1);public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Audio:ILiveRecordingAudioFactory
    {
        public Source? Latest;public int Opened;
        public Task<ILiveRecordingAudioSource> OpenLiveAsync(RecordingOptions options,RecordingSourceChoices sources,CancellationToken token){Opened++;Latest=new(){MicrophoneEnabled=sources.Microphone};return Task.FromResult<ILiveRecordingAudioSource>(Latest);}
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token)=>throw new InvalidOperationException();
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>throw new InvalidOperationException();
    }
    private sealed class Source:ILiveRecordingAudioSource
    {
        public bool MicrophoneEnabled{get;set;}public bool Disposed,FailEnable;
        public Task SetMicrophoneEnabledAsync(bool value,CancellationToken token){if(FailEnable)throw new IOException("mic denied");MicrophoneEnabled=value;return Task.CompletedTask;}
        public IReadOnlyList<string> Arguments=>["-f","f32le","-i","fixture"];public Task Completion=>Task.Delay(Timeout.Infinite);
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private sealed class Assembler:IRecordingAssembler
    {
        public IReadOnlyList<RecordingSegment>? Segments;public bool KeepAudio,Hold;public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RecordingResult> AssembleAsync(RecordingOptions options,RecordingWorkspace workspace,IReadOnlyList<RecordingSegment> segments,bool keepAudio,CancellationToken token)
        {
            Segments=segments.ToArray();KeepAudio=keepAudio;Started.TrySetResult();if(Hold)await Task.Delay(Timeout.Infinite,token);
            await File.WriteAllTextAsync(workspace.CapturePath,"assembled",token);return new(true,workspace.CapturePath,null,null);
        }
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
