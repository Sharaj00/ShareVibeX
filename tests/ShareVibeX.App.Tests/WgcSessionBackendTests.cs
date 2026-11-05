using ShareVibeX.App.Recording;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;

public sealed class WgcSessionBackendTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"ShareVibeX-session-backend",Guid.NewGuid().ToString("N"));
    [Fact] public async Task ServiceReadsPreferenceForEachNewSessionAndReportsEffectiveMethod()
    {
        var fixture=new Fixture(root);
        await fixture.Service.StartAsync(new(new(),new(0,0,100,100),new(false,false)),default);
        Assert.Equal(CaptureBackend.WindowsGraphicsCapture,fixture.Session.EffectiveCaptureBackend);Assert.Contains("WGC",fixture.Service.Status);
        fixture.Preferred=CaptureBackend.Gdi;await fixture.PauseAsync();await fixture.Session.ResumeAsync();
        Assert.Contains("lavfi",fixture.Factory.Arguments.Last());await fixture.Session.CancelAsync();
        await fixture.Service.StartAsync(new(new(),new(0,0,100,100),new(false,false)),default);
        Assert.Equal(CaptureBackend.Gdi,fixture.Session.EffectiveCaptureBackend);Assert.Contains("GDI",fixture.Service.Status);
        await fixture.Session.CancelAsync();
    }
    [Fact] public async Task AutomaticWgcRemainsStrictAcrossFailedResumeAndRetry()
    {
        var fixture=new Fixture(root);
        await fixture.Session.StartAsync(new(),new(0,0,100,100),new(false,false),captureBackend:CaptureBackend.Automatic);
        await fixture.PauseAsync();fixture.Unavailable=true;
        await Assert.ThrowsAsync<CaptureSourceUnavailableException>(()=>fixture.Session.ResumeAsync());
        Assert.Equal(RecordingSessionState.Paused,fixture.Session.State);Assert.Single(fixture.Session.Segments);
        Assert.Single(fixture.Factory.Arguments);
        fixture.Unavailable=false;await fixture.Session.ResumeAsync();
        Assert.All(fixture.Factory.Arguments,args=>Assert.Contains("lavfi",args));
        await fixture.Session.CancelAsync();
    }
    [Fact] public async Task AutomaticGdiDoesNotReselectUntilANewSession()
    {
        var fixture=new Fixture(root){Unavailable=true};
        await fixture.Session.StartAsync(new(),new(0,0,100,100),new(false,false),captureBackend:CaptureBackend.Automatic);
        await fixture.PauseAsync();fixture.Unavailable=false;await fixture.Session.ResumeAsync();
        Assert.Equal(1,fixture.Resolutions);Assert.All(fixture.Factory.Arguments,args=>Assert.Contains("gdigrab",args));
        await fixture.Session.CancelAsync();
        await fixture.Session.StartAsync(new(),new(0,0,100,100),new(false,false),captureBackend:CaptureBackend.Automatic);
        Assert.Equal(2,fixture.Resolutions);Assert.Contains("lavfi",fixture.Factory.Arguments.Last());
        await fixture.Session.CancelAsync();
    }
    [Fact] public async Task InvalidBackendHasNoSessionOrFilesystemSideEffects()
    {
        var fixture=new Fixture(root);var changes=0;fixture.Session.Changed+=()=>changes++;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>fixture.Session.StartAsync(new(),new(0,0,100,100),new(false,false),captureBackend:(CaptureBackend)99));
        Assert.Equal(RecordingSessionState.Idle,fixture.Session.State);Assert.Null(fixture.Session.Workspace);
        Assert.Null(fixture.Session.Completion);Assert.Equal(0,changes);Assert.Empty(fixture.Factory.Arguments);Assert.False(Directory.Exists(root));
    }
    [Fact] public async Task CustomTemplateKeepsItsGdiInputWhenWgcIsSelected()
    {
        var fixture=new Fixture(root);
        await fixture.Session.StartAsync(new(){UseCustomCommand=true},new(0,0,100,100),new(false,false),captureBackend:CaptureBackend.WindowsGraphicsCapture);
        Assert.Equal(0,fixture.Resolutions);Assert.Contains("gdigrab",fixture.Factory.Arguments.Single());
        Assert.DoesNotContain("lavfi",fixture.Factory.Arguments.Single());await fixture.Session.CancelAsync();
    }
    private sealed class Fixture
    {
        public readonly Factory Factory=new();public readonly RecordingSessionController Session;
        public readonly RecordingService Service;public CaptureBackend Preferred=CaptureBackend.Automatic;
        public bool Unavailable;public int Resolutions;
        public Fixture(string root)
        {
            var engine=new RecordingEngine(new Probe(),Factory,new Audio(),_=>"fixture",resolveWgc:(region,options)=>
            {
                Resolutions++;if(Unavailable)throw new CaptureSourceUnavailableException("SDR source unavailable");
                return WgcVideoSource.Create(region,[new(new("monitor",new(0,0,100,100),1),42)],options.FrameRate,options.IncludeCursor);
            });
            Service=new(engine,root,()=>Preferred);Session=Service.Session;
        }
        public async Task PauseAsync(){await File.WriteAllTextAsync(Factory.Arguments.Last().Last(),"clip");await Session.PauseAsync();}
    }
    private sealed class Probe:ICommandRunner
    {
        public Task<ToolResult> RunAsync(string exe,IReadOnlyList<string> args,CancellationToken token)=>Task.FromResult(new ToolResult(0,args.Last() switch
        {
            "-version"=>"ffmpeg version fixture",
            "-devices"=>" D  gdigrab input\n D  lavfi input",
            "-encoders"=>" V....D libx264 video",
            "encoder=libx264"=>"Encoder libx264 [H264]:\n -preset <string> E..V....... preset\n -tune <string> E..V....... tune\n -crf <float> E..V....... quality (from -1 to 51)\n",
            "-filters"=>string.Join('\n',new[]{"pad","format","gfxcapture","hwdownload","setpts","fps"}.Select(name=>$" .. {name} V->V")),
            "filter=gfxcapture"=>string.Join('\n',new[]{"hmonitor","capture_cursor","max_framerate","crop_left","crop_top","crop_right","crop_bottom","width","height","resize_mode","output_fmt"}.Select(name=>$"  {name} <int> value")),
            _=>""
        },""));
    }
    private sealed class Factory:IRecordingProcessFactory
    {
        public readonly List<IReadOnlyList<string>> Arguments=[];
        public IRecordingProcess Start(string exe,IReadOnlyList<string> args){Arguments.Add(args);return new Process();}
    }
    private sealed class Process:IRecordingProcess
    {
        private readonly TaskCompletionSource<int> ended=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Ready=>Task.CompletedTask;public Task<int> Exit=>ended.Task;
        public RecordingProgress Progress=>new(TimeSpan.FromSeconds(2),4,60);public string Diagnostics=>"";
        public Task RequestStopAsync(){ended.TrySetResult(0);return Task.CompletedTask;}
        public void Kill()=>ended.TrySetResult(-1);public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Audio:IRecordingAudioFactory
    {
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token)=>throw new InvalidOperationException("No audio expected");
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>throw new InvalidOperationException("No devices expected");
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
