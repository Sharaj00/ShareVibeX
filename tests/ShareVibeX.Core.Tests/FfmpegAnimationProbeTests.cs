using ShareVibeX.Core.Recording;
namespace ShareVibeX.Core.Tests;
public sealed class FfmpegAnimationProbeTests
{
    [Theory][InlineData(VideoEncoder.Gif)][InlineData(VideoEncoder.LibwebpAnim)][InlineData(VideoEncoder.Apng)]
    public async Task ListedAnimationPipelineIsCheckedWithoutOpeningMedia(VideoEncoder encoder)
    {
        var runner=new Runner();await new FfmpegAnimationProbe(runner).ValidateAsync("fixture",new RecordingOptions().SwitchEncoder(encoder));
        Assert.Equal(encoder==VideoEncoder.Gif?4:3,runner.Calls.Count);Assert.DoesNotContain(runner.Calls.SelectMany(a=>a),a=>a is "-i" or "-list_devices");
    }
    [Fact] public async Task StaticWebpSupportDoesNotProveAnimatedPreview()
    {
        var runner=new Runner{Animated=false};
        var error=await Assert.ThrowsAsync<NotSupportedException>(()=>new FfmpegAnimationProbe(runner).ValidateAsync("fixture",new RecordingOptions().SwitchEncoder(VideoEncoder.LibwebpAnim)));
        Assert.Contains("webp_anim",error.Message);
    }
    [Fact] public async Task MissingIntermediateReaderFailsBeforeRecording()
    {
        var error=await Assert.ThrowsAsync<NotSupportedException>(()=>new FfmpegAnimationProbe(new Runner{Matroska=false}).ValidateAsync("fixture",new RecordingOptions().SwitchEncoder(VideoEncoder.Gif)));
        Assert.Contains("demuxer=matroska",error.Message);
    }
    private sealed class Runner:ICommandRunner
    {
        public bool Animated=true,Matroska=true;public List<IReadOnlyList<string>> Calls=[];
        public Task<ToolResult> RunAsync(string path,IReadOnlyList<string> args,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Calls.Add(args);
            var text=args.Last() switch
            {
                "-filters"=>" .. split V->N\n ... palettegen V->V\n .. paletteuse VV->V\n",
                "-muxers"=>" E gif muxer\n E apng muxer\n E webp muxer\n E matroska muxer\n",
                "-demuxers"=>(Matroska?" D matroska,webm demuxer\n":"")+" D gif demuxer\n D apng demuxer\n"+(Animated?" D webp_anim demuxer\n":" D webp_pipe demuxer\n"),
                "-decoders"=>" V..... ffv1 decoder\n V..... gif decoder\n V..... apng decoder\n"+(Animated?" V..... webp_anim decoder\n":" V..... webp decoder\n"),
                _=>throw new Exception("Unexpected query")
            };
            return Task.FromResult(new ToolResult(0,"",text));
        }
    }
}
