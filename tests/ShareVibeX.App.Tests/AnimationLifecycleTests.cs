using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;
namespace ShareVibeX.App.Tests;
public sealed class AnimationLifecycleTests
{
    [Theory]
    [InlineData(VideoEncoder.Gif,false)][InlineData(VideoEncoder.Gif,true)]
    [InlineData(VideoEncoder.LibwebpAnim,false)][InlineData(VideoEncoder.LibwebpAnim,true)]
    [InlineData(VideoEncoder.Apng,false)][InlineData(VideoEncoder.Apng,true)]
    public async Task CaptureUsesSilentLosslessSegmentsAndFinalEncodingAfterStop(VideoEncoder encoder,bool pause)
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-animation",Guid.NewGuid().ToString("N"));
        try
        {
            var factory=new Factory();var options=new RecordingOptions().SwitchEncoder(encoder);
            var engine=new RecordingEngine(new Probe(),factory,new Audio(),_=>"fixture");
            var session=new RecordingSessionController(engine,root);
            await session.StartAsync(options,new(0,0,101,99),new(true,true));
            Assert.Equal(new(false,false),session.Sources);Assert.False(session.CanToggleMicrophone);
            Assert.Contains("ffv1",factory.Calls.Single());Assert.Contains("bgr0",factory.Calls.Single());Assert.Contains("-an",factory.Calls.Single());
            Assert.EndsWith(".mkv",factory.Calls.Single().Last());
            if(pause){await session.PauseAsync();await session.ResumeAsync();}
            await session.StopAsync();
            Assert.True(session.Result!.Success,session.Result.Error);Assert.Equal(RecordingSessionState.Result,session.State);
            Assert.Contains(options.EncoderName,factory.Calls.Last());Assert.DoesNotContain("copy",factory.Calls.Last());
            Assert.EndsWith("."+options.Extension,session.Result.OutputPath);Assert.False(session.CreatePreviewMedia().HasAudio);
            Assert.Equal(pause?3:2,factory.Calls.Count);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private sealed class Probe:ICommandRunner
    {
        public Task<ToolResult> RunAsync(string path,IReadOnlyList<string> args,CancellationToken token)
        {
            var command=args.Last();var text=command switch
            {
                "-version"=>"ffmpeg version fixture","-devices"=>" D  gdigrab input",
                "-encoders"=>string.Join('\n',new[]{"ffv1","gif","libwebp_anim","apng"}.Select(n=>" V....D "+n+" encoder")),
                "-filters"=>" .. pad V->V\n .. format V->V\n .. split V->N\n .. palettegen V->V\n .. paletteuse VV->V\n",
                "-muxers"=>" E matroska muxer\n E gif muxer\n E webp muxer\n E apng muxer\n",
                "-demuxers"=>" D matroska,webm demuxer\n D gif demuxer\n D webp_anim demuxer\n D apng demuxer\n",
                "-decoders"=>" V..... gif decoder\n V..... webp_anim decoder\n V..... apng decoder\n V..... ffv1 decoder\n",
                _ when command.StartsWith("encoder=")=>"Encoder "+command[8..]+":\n -quality <float> E..V....... (from 0 to 100)\n",
                _=>throw new Exception("Unexpected query")
            };
            return Task.FromResult(new ToolResult(0,text,""));
        }
    }
    private sealed class Factory:IRecordingProcessFactory
    {
        public List<IReadOnlyList<string>> Calls=[];
        public IRecordingProcess Start(string path,IReadOnlyList<string> args)
        {
            Calls.Add(args);File.WriteAllText(args.Last(),"synthetic frames");return new Process(!args.Last().EndsWith(".mkv",StringComparison.Ordinal));
        }
    }
    private sealed class Process:IRecordingProcess
    {
        private readonly TaskCompletionSource<int> exit=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool final;
        public Process(bool final){this.final=final;if(final)exit.SetResult(0);}
        public Task Ready=>final?new TaskCompletionSource().Task:Task.CompletedTask;
        public Task<int> Exit=>exit.Task;public string Diagnostics=>"";
        public RecordingProgress Progress=>new(TimeSpan.FromSeconds(2),16,60);
        public Task RequestStopAsync(){exit.TrySetResult(0);return Task.CompletedTask;}
        public void Kill()=>exit.TrySetResult(-1);public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Audio:ILiveRecordingAudioFactory
    {
        public Task<ILiveRecordingAudioSource> OpenLiveAsync(RecordingOptions options,RecordingSourceChoices sources,CancellationToken token)=>throw new Exception("Animation must not open live audio");
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token)=>throw new Exception("Animation must not open audio");
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>throw new Exception("Animation must not query devices");
    }
}
