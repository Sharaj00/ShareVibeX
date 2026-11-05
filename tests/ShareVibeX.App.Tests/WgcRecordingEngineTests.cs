using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Tests;

public sealed class WgcRecordingEngineTests:IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"ShareVibeX-wgc-engine",Guid.NewGuid().ToString("N"));
    private static readonly PixelRect Region=new(0,0,100,100);
    private static WgcVideoSource Resolve(PixelRect region,RecordingOptions options)=>WgcVideoSource.Create(region,[new(new("screen",Region,1),1)],options.FrameRate,options.IncludeCursor);
    private RecordingRequest Request(CaptureBackend backend,bool audio=false)=>new(new(),Region,Path.Combine(directory,"clip.mp4"),audio){CaptureBackend=backend};
    [Fact] public async Task WgcStartsWithoutGdigrabAndReportsEffectiveSource()
    {
        var factory=new Factory();var run=await new RecordingEngine(new Probe{Gdi=false},factory,new Audio(),_=>"fixture",resolveWgc:Resolve).StartAsync(Request(CaptureBackend.WindowsGraphicsCapture));
        Assert.Equal(CaptureBackend.WindowsGraphicsCapture,run.EffectiveCaptureBackend);Assert.Contains("lavfi",factory.Arguments!);Assert.DoesNotContain("gdigrab",factory.Arguments!);
        Assert.Null(run.CaptureFallbackReason);await Finish(factory,run);
    }
    [Fact] public async Task AutomaticMissingWgcFallsBackBeforeAudioOrProcessStarts()
    {
        var factory=new Factory();var resolved=false;
        var run=await new RecordingEngine(new Probe{Wgc=false},factory,new Audio(),_=>"fixture",resolveWgc:(r,o)=>{resolved=true;return Resolve(r,o);}).StartAsync(Request(CaptureBackend.Automatic));
        Assert.False(resolved);Assert.Equal(CaptureBackend.Gdi,run.EffectiveCaptureBackend);Assert.NotNull(run.CaptureFallbackReason);Assert.Contains("gdigrab",factory.Arguments!);await Finish(factory,run);
    }
    [Fact] public async Task AutomaticUnsupportedColorFallsBackButStrictSourceDoesNot()
    {
        foreach(var backend in new[]{CaptureBackend.Automatic,CaptureBackend.WindowsGraphicsCapture})
        {
            var factory=new Factory();var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture",resolveWgc:(_,_)=>throw new CaptureSourceUnavailableException("HDR"));
            if(backend==CaptureBackend.Automatic){var run=await engine.StartAsync(Request(backend));Assert.Equal("HDR",run.CaptureFallbackReason);await Finish(factory,run);}
            else{await Assert.ThrowsAsync<CaptureSourceUnavailableException>(()=>engine.StartAsync(Request(backend)));Assert.Equal(0,factory.Starts);}
        }
    }
    [Fact] public async Task InvalidSourceNeverFallsBackAndDoesNotOpenAudio()
    {
        var factory=new Factory();var audio=new Audio();
        var engine=new RecordingEngine(new Probe(),factory,audio,_=>"fixture",resolveWgc:(_,_)=>throw new InvalidDataException("topology"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>engine.StartAsync(Request(CaptureBackend.Automatic,true)));Assert.Equal(0,audio.Opened);Assert.Equal(0,factory.Starts);
    }
    [Fact] public async Task MissingEncoderNeverTriggersSourceRetry()
    {
        var factory=new Factory();var audio=new Audio();var resolves=0;
        var engine=new RecordingEngine(new Probe{Encoder=false},factory,audio,_=>"fixture",resolveWgc:(r,o)=>{resolves++;return Resolve(r,o);});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>engine.StartAsync(Request(CaptureBackend.Automatic,true)));Assert.Equal(0,resolves);Assert.Equal(0,audio.Opened);Assert.Equal(0,factory.Starts);
    }
    [Fact] public async Task MissingSvtEncoderIsReportedBeforeHelpSourceAudioOrProcess()
    {
        var probe=new Probe();var factory=new Factory();var audio=new Audio();
        var engine=new RecordingEngine(probe,factory,audio,_=>"fixture",resolveWgc:(_,_)=>throw new Exception("Must not resolve"));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>engine.StartAsync(Request(CaptureBackend.Automatic,true) with{Options=new(){Encoder=VideoEncoder.Libsvtav1}}));
        Assert.Contains("libsvtav1",error.Message);Assert.Equal(0,probe.EncoderHelpCalls);Assert.Equal(0,audio.Opened);Assert.Equal(0,factory.Starts);
    }
    [Fact] public async Task ProcessStartupErrorNeverRetriesUsingGdi()
    {
        var factory=new Factory{FailStart=true};
        var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture",resolveWgc:Resolve);
        await Assert.ThrowsAsync<IOException>(()=>engine.StartAsync(Request(CaptureBackend.Automatic)));Assert.Equal(1,factory.Starts);Assert.DoesNotContain("gdigrab",factory.Arguments!);
    }
    [Fact] public async Task CustomTemplateRemainsExplicitAndDoesNotResolveWgc()
    {
        var factory=new Factory();var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture",resolveWgc:(_,_)=>throw new Exception("Must not resolve"));
        var run=await engine.StartAsync(Request(CaptureBackend.WindowsGraphicsCapture) with{Options=new(){UseCustomCommand=true}});
        Assert.Equal(CaptureBackend.Gdi,run.EffectiveCaptureBackend);Assert.Contains("gdigrab",factory.Arguments!);await Finish(factory,run);
    }
    [Fact] public async Task CancellationAfterResolutionNeverStartsFallbackOrProcess()
    {
        using var cancel=new CancellationTokenSource();var factory=new Factory();
        var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture",resolveWgc:(r,o)=>{cancel.Cancel();return Resolve(r,o);});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>engine.StartAsync(Request(CaptureBackend.Automatic),cancel.Token));Assert.Equal(0,factory.Starts);
    }
    [Fact] public async Task StrictMissingWgcDoesNotStartGdi()
    {
        var factory=new Factory();
        await Assert.ThrowsAsync<CaptureSourceUnavailableException>(()=>new RecordingEngine(new Probe{Wgc=false},factory,new Audio(),_=>"fixture",resolveWgc:Resolve)
            .StartAsync(Request(CaptureBackend.WindowsGraphicsCapture)));Assert.Equal(0,factory.Starts);
    }
    [Fact] public async Task InvalidEncodingOptionsNeverResolveNativeSource()
    {
        var resolved=false;var factory=new Factory();
        var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture",resolveWgc:(r,o)=>{resolved=true;return Resolve(r,o);});
        await Assert.ThrowsAsync<ArgumentException>(()=>engine.StartAsync(Request(CaptureBackend.Automatic) with{Options=new(){Preset="invalid"}}));
        Assert.False(resolved);Assert.Equal(0,factory.Starts);
    }
    private static async Task Finish(Factory factory,RecordingRun run)
    {await File.WriteAllTextAsync(factory.Arguments!.Last(),"synthetic video");factory.Process.Exited.TrySetResult(0);Assert.True((await run.Completion).Success);}
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private sealed class Factory:IRecordingProcessFactory
    {
        public RecordingEngineTests.FakeProcess Process=new();public IReadOnlyList<string>? Arguments;public int Starts;public bool FailStart;
        public IRecordingProcess Start(string exe,IReadOnlyList<string> args){Starts++;Arguments=args;if(FailStart)throw new IOException("encoder startup");return Process;}
    }
    private sealed class Audio:IRecordingAudioFactory
    {
        public int Opened;
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token){Opened++;throw new IOException("synthetic audio error");}
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>throw new Exception("Must not enumerate");
    }
    private sealed class Probe:ICommandRunner
    {
        public bool Wgc=true,Gdi=true,Encoder=true;
        public int EncoderHelpCalls;
        public Task<ToolResult> RunAsync(string exe,IReadOnlyList<string> args,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(args.Last().StartsWith("encoder=",StringComparison.Ordinal))EncoderHelpCalls++;
            var text=args.Last() switch
            {
                "-version"=>"ffmpeg version fixture",
                "-devices"=>(Gdi?" D  gdigrab desktop\n":"")+" D  lavfi filters\n",
                "-encoders"=>(Encoder?" V....D libx264 H264\n":"")+" A....D aac audio\n",
                "encoder=libx264"=>"Encoder libx264 [H264]:\n -preset <string> E..V....... preset\n -tune <string> E..V....... tune\n -crf <float> E..V....... quality (from -1 to 51)\n",
            "-filters"=>string.Join('\n',new[]{"format","pad","scale","fps","setpts","hwdownload","color","overlay"}.Select(f=>" .. "+f+" V->V"))+(Wgc?"\n .. gfxcapture |->V":""),
                "filter=gfxcapture"=>string.Join('\n',new[]{"hmonitor","capture_cursor","max_framerate","crop_left","crop_top","crop_right","crop_bottom","width","height","resize_mode","output_fmt"}.Select(o=>"   "+o+" <int> ..FV....... option")),
                _=>throw new InvalidOperationException()
            };
            return Task.FromResult(new ToolResult(0,text,""));
        }
    }
}
