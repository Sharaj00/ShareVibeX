using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Tests;

public sealed class RecordingSessionTests : IDisposable
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task StopOrCancelDuringCountdownReleasesStartup(bool cancel)
    {
        var factory=new Factory();var session=Create(factory,new());
        var starting=session.StartAsync(new(),new(0,0,100,100),new(false,false),default,10);
        Assert.True(session.IsActive);await (cancel?session.CancelAsync():session.StopAsync()).WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>starting);Assert.Null(factory.Path);Assert.False(session.IsActive);
    }
    [Fact]
    public async Task RecordingEnginePreparesAudioWhileWaitingForStartGate()
    {
        var factory=new Factory();var audio=new Audio();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine=new RecordingEngine(new Probe(),factory,audio,_=>"ffmpeg.exe");
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var start=engine.StartAsync(new(new(),new(0,0,100,100),Path.Combine(root,"test.mp4"),true),cancellation.Token,gate.Task);
        await audio.Opened.Task.WaitAsync(cancellation.Token);
        Assert.False(factory.Started.Task.IsCompleted);
        gate.TrySetResult();
        var run=await start;
        await File.WriteAllTextAsync(factory.Path!,"video");
        Assert.True((await run.StopAsync()).Success);
    }
    private readonly string root=Path.Combine(Path.GetTempPath(),"ShareVibeX-session-tests",Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData(false,false,null)] [InlineData(true,false,AudioSource.System)]
    [InlineData(false,true,AudioSource.Microphone)] [InlineData(true,true,AudioSource.SystemAndMicrophone)]
    public async Task ImmediateStartUsesOwnedPathsAndIndependentSources(bool sound,bool mic,AudioSource? expected)
    {
        var factory=new Factory();var audio=new Audio();var controller=Create(factory,audio);
        await controller.StartAsync(new(){PlaybackDeviceId="speakers",MicrophoneDeviceId="mic"},new(3,4,101,99),new(sound,mic));
        Assert.Equal(RecordingSessionState.Recording,controller.State);
        Assert.Equal(expected,audio.Options?.AudioSource);
        if(expected is not null){Assert.Equal("speakers",audio.Options!.PlaybackDeviceId);Assert.Equal("mic",audio.Options.MicrophoneDeviceId);}
        Assert.Equal(102,controller.Width);Assert.Equal(100,controller.Height);
        Assert.StartsWith(Path.GetFullPath(root),factory.Path);Assert.True(File.Exists(controller.Workspace!.ManifestPath));
        await File.WriteAllTextAsync(factory.Path!,"video");var first=controller.StopAsync();var second=controller.StopAsync();
        await Task.WhenAll(first,second);Assert.Equal(1,factory.Process.Stops);
        Assert.Equal(RecordingSessionState.Result,controller.State);Assert.True(File.Exists(controller.Result!.OutputPath));
    }
    [Fact]
    public async Task CancelDeletesOnlyKnownSessionFilesAfterProcessDisposal()
    {
        var factory=new Factory();var controller=Create(factory,new());
        await controller.StartAsync(new(),new(0,0,100,100),new(false,false));
        var unrelated=Path.Combine(controller.Workspace!.DirectoryPath,"unrelated.txt");await File.WriteAllTextAsync(unrelated,"keep");
        await File.WriteAllTextAsync(factory.Path!,"video");
        await controller.CancelAsync();Assert.True(factory.Process.Disposed);
        Assert.Equal("keep",await File.ReadAllTextAsync(unrelated));Assert.False(File.Exists(factory.Path));
        Assert.False(File.Exists(controller.Workspace!.CapturePath));Assert.Equal(RecordingSessionState.Idle,controller.State);
    }
    [Fact]
    public async Task StopDuringStartupCancelsAndReleasesEngineForRetry()
    {
        var factory=new Factory();factory.Process.PendingReady=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller=Create(factory,new());var starting=controller.StartAsync(new(),new(0,0,100,100),new(false,false));
        await factory.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stopping=controller.StopAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>starting);await stopping;
        Assert.True(factory.Process.Disposed);Assert.Equal(RecordingSessionState.Idle,controller.State);
    }
    [Fact]
    public async Task NaturalExitAndRepeatedStopPublishExactlyOnePendingResult()
    {
        var factory=new Factory();var controller=Create(factory,new());var completions=0;controller.Finished+=_=>completions++;
        await controller.StartAsync(new(),new(0,0,100,100),new(false,false));
        await File.WriteAllTextAsync(factory.Path!,"video");factory.Process.ExitSource.TrySetResult(0);
        await controller.Completion!;await controller.StopAsync();await controller.StopAsync();
        Assert.Equal(1,completions);Assert.True(controller.HasPendingResult);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>controller.StartAsync(new(),new(0,0,100,100),new(false,false)));
    }
    [Fact]
    public async Task FailureRetainsPartialAndSourceChoicesForRetry()
    {
        var factory=new Factory();var controller=Create(factory,new());var choices=new RecordingSourceChoices(false,false);
        await controller.StartAsync(new(),new(20,30,100,100),choices);
        await File.WriteAllTextAsync(factory.Path!,"recoverable");factory.Process.ExitSource.TrySetResult(1);
        await controller.Completion!;
        Assert.Equal(RecordingSessionState.Failed,controller.State);Assert.True(File.Exists(controller.Result!.PartialPath));
        Assert.Equal(choices,controller.Sources);Assert.Equal(new PixelRect(20,30,100,100),controller.Region);
    }
    [Fact]
    public async Task WorkspaceDoesNotTrustPathsInItsManifestAndNeverDeletesOtherSessions()
    {
        var one=RecordingWorkspace.Create(root,new(),new(0,0,10,10));var two=RecordingWorkspace.Create(root,new(),new(0,0,10,10));
        await File.WriteAllTextAsync(two.CapturePath,"other video");
        await File.WriteAllTextAsync(one.ManifestPath,"{\"files\":[\""+two.CapturePath.Replace("\\","\\\\")+"\"]}");
        await one.DiscardAsync();Assert.Equal("other video",await File.ReadAllTextAsync(two.CapturePath));
    }
    [Fact]
    public async Task DirectShowAndNativeMicCombinationFailsBeforeAnyProcess()
    {
        var factory=new Factory();var controller=Create(factory,new());
        await Assert.ThrowsAsync<NotSupportedException>(()=>controller.StartAsync(new(){AudioSource=AudioSource.DirectShow,DirectShowDevice="legacy"},new(0,0,100,100),new(true,true)));
        Assert.Null(factory.Path);
    }
    [Fact]
    public async Task CancelingFailedSessionPreventsRetryUntilItsFilesAreReleased()
    {
        var factory=new Factory();var controller=Create(factory,new());
        await controller.StartAsync(new(),new(0,0,100,100),new(false,false));
        await File.WriteAllTextAsync(factory.Path!,"partial");factory.Process.ExitSource.TrySetResult(1);await controller.Completion!;
        var cancel=controller.CancelAsync();
        try{await Assert.ThrowsAsync<InvalidOperationException>(()=>controller.StartAsync(new(),new(0,0,100,100),new(false,false)));}
        finally{await cancel;}
        Assert.Equal(RecordingSessionState.Idle,controller.State);
    }
    [Fact]
    public async Task CleanupFailureDuringStartupIsRecoverableAndCancelCanBeRetried()
    {
        if(!OperatingSystem.IsWindows())return;
        var factory=new Factory();factory.Process.PendingReady=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller=Create(factory,new());var starting=controller.StartAsync(new(),new(0,0,100,100),new(false,false));
        await factory.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using(var locked=new FileStream(factory.Path!,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
        {
            await locked.WriteAsync(new byte[]{1});await locked.FlushAsync();
            var cancel=controller.CancelAsync();
            await Assert.ThrowsAnyAsync<Exception>(()=>starting);await Assert.ThrowsAnyAsync<IOException>(()=>cancel);
            Assert.Equal(RecordingSessionState.Failed,controller.State);Assert.False(controller.IsActive);
        }
        await controller.CancelAsync();Assert.Equal(RecordingSessionState.Idle,controller.State);Assert.False(File.Exists(factory.Path));
    }
    private RecordingSessionController Create(Factory factory,Audio audio)=>new(new(new Probe(),factory,audio,_=>"ffmpeg.exe",stopTimeout:TimeSpan.FromMilliseconds(100)),root);
    private sealed class Probe:ICommandRunner
    {
        public Task<ToolResult> RunAsync(string executable,IReadOnlyList<string> args,CancellationToken token)=>Task.FromResult(new ToolResult(0,args.Last() switch
        {"-version"=>"ffmpeg version fixture","-devices"=>" D  gdigrab input\n DE dshow input","-encoders"=>" V....D libx264 video\n A....D aac audio","encoder=libx264"=>"Encoder libx264 [H264]:\n -preset <string> E..V....... preset\n -tune <string> E..V....... tune\n -crf <float> E..V....... quality (from -1 to 51)\n",
            "-filters"=>" .. pad V->V\n .. format V->V",_=>""},""));
    }
    private sealed class Factory:IRecordingProcessFactory
    {
        public string? Path;public Process Process=new();public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IRecordingProcess Start(string executable,IReadOnlyList<string> args){Path=args.Last();Started.TrySetResult();return Process;}
    }
    private sealed class Process:IRecordingProcess
    {
        public TaskCompletionSource? PendingReady;public Task Ready=>PendingReady?.Task??Task.CompletedTask;
        public TaskCompletionSource<int> ExitSource=new(TaskCreationOptions.RunContinuationsAsynchronously);public Task<int> Exit=>ExitSource.Task;
        public string Diagnostics=>"fixture failure";public int Stops;public bool Disposed;
        public Task RequestStopAsync(){Stops++;ExitSource.TrySetResult(0);return Task.CompletedTask;}
        public void Kill()=>ExitSource.TrySetResult(-1);
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private sealed class Audio:IRecordingAudioFactory
    {
        public RecordingOptions? Options;
        public TaskCompletionSource Opened {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token){Options=options;Opened.TrySetResult();return Task.FromResult<IRecordingAudioSource>(new Source());}
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>throw new InvalidOperationException("No device enumeration permitted.");
    }
    private sealed class Source:IRecordingAudioSource
    {
        public IReadOnlyList<string> Arguments=>["-f","f32le","-i","fixture"];
        public Task Completion=>Task.Delay(Timeout.Infinite);public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
