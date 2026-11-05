using ShareVibeX.Core.Recording;
namespace ShareVibeX.Core.Tests;
public sealed class FfmpegEncoderProbeTests
{
    [Fact] public async Task EncoderHelpDoesNotDependOnAnInventedCaptureSize()
    {
        await new FfmpegEncoderProbe(new Runner(Help)).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1,ScaleMode=ScaleMode.Percentage,ScalePercent=0.01});
    }
    [Theory][InlineData(VideoEncoder.Libvpx)][InlineData(VideoEncoder.LibvpxVp9)][InlineData(VideoEncoder.LibaomAv1)]
    public async Task HyphenatedPrivateOptionNamesAreRecognized(VideoEncoder encoder)
    {
        var options=new RecordingOptions{Encoder=encoder,Container=VideoContainer.Matroska};
        var help="Encoder "+options.EncoderName+":\n -cpu-used <int> E..V....... (from 0 to 8)\n -crf <int> E..V....... (from 0 to 63)\n";
        await new FfmpegEncoderProbe(new Runner(help)).ValidateAsync("fixture",options);
    }
    [Fact] public async Task HeaderWithoutDescriptionIsRecognized()
    {await new FfmpegEncoderProbe(new Runner(Help.Replace("Encoder libsvtav1 [SVT-AV1 encoder]:","Encoder libsvtav1:"))).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1});}
    [Theory]
    [InlineData(VideoEncoder.Libx264)] [InlineData(VideoEncoder.Libx265)]
    [InlineData(VideoEncoder.H264Nvenc)] [InlineData(VideoEncoder.HevcNvenc)]
    [InlineData(VideoEncoder.H264Qsv)] [InlineData(VideoEncoder.H264Amf)]
    public async Task EstablishedEncodersValidateTheirEmittedPrivateOptions(VideoEncoder encoder)
    {
        var options=new RecordingOptions{Encoder=encoder};
        var help="Encoder "+options.EncoderName+" [fixture]:\n -preset <string> E..V.......\n -tune <string> E..V.......\n -crf <float> E..V....... (from -1 to 51)\n -quality <int> E..V....... (from 0 to 2)\n   balanced 1 E..V.......\n -rc <int> E..V....... (from 0 to 3)\n   cqp 0 E..V.......\n   vbr 1 E..V.......\n -cq <float> E..V....... (from 0 to 51)\n -qp_i <int> E..V....... (from -1 to 51)\n -qp_p <int> E..V....... (from -1 to 51)\n";
        await new FfmpegEncoderProbe(new Runner(help)).ValidateAsync("fixture",options);
    }
    private const string Help="Encoder libsvtav1 [SVT-AV1 encoder]:\n  -preset <int> E..V....... speed (from -2 to 8) (default -2)\n  -crf <int> E..V....... quality (from 0 to 63) (default 35)\n";
    [Theory][InlineData(false)][InlineData(true)]
    public async Task TypedOptionsAndRangesAreReadWithoutOpeningAnInput(bool stderr)
    {
        var fake=new Runner(Help,stderr);await new FfmpegEncoderProbe(fake).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1,Preset="8",Quality=63});
        Assert.Equal(new[]{"-hide_banner","-h","encoder=libsvtav1"},fake.Arguments);Assert.DoesNotContain("-i",fake.Arguments!);
    }
    [Fact] public async Task OlderBuildRejectsUnsupportedSpeed()
    {await Assert.ThrowsAsync<ArgumentException>(()=>new FfmpegEncoderProbe(new Runner(Help)).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1,Preset="13"}));}
    [Fact] public async Task MissingOptionIsNotInferredFromProse()
    {await Assert.ThrowsAsync<ArgumentException>(()=>new FfmpegEncoderProbe(new Runner("Encoder libsvtav1 [encoder]:\n crf is described here\n")).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1}));}
    [Fact] public async Task UnknownEncoderHelpWithSuccessfulExitIsRejected()
    {await Assert.ThrowsAsync<InvalidDataException>(()=>new FfmpegEncoderProbe(new Runner("Unknown encoder libsvtav1")).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1}));}
    [Fact] public async Task CancellationAfterQueryIsPreserved()
    {
        using var cancel=new CancellationTokenSource();var fake=new Runner(Help){OnQuery=cancel.Cancel};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new FfmpegEncoderProbe(fake).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1},cancel.Token));
    }
    [Theory][InlineData("balanced",true)][InlineData("quality",false)]
    public async Task NamedConstantsMustBelongToTheirOwnOption(string preset,bool supported)
    {
        const string help="Encoder hevc_amf [HEVC]:\n -quality <int> E..V....... quality (from 0 to 2)\n    speed 0 E..V.......\n    balanced 1 E..V.......\n -rc <int> E..V....... mode (from 0 to 3)\n    cqp 0 E..V.......\n    quality 2 E..V....... unrelated constant\n -qp_i <int> E..V....... (from -1 to 51)\n -qp_p <int> E..V....... (from -1 to 51)\n";
        var task=new FfmpegEncoderProbe(new Runner(help)).ValidateAsync("fixture",new(){Encoder=VideoEncoder.HevcAmf,Preset=preset});
        if(supported)await task;else await Assert.ThrowsAsync<ArgumentException>(()=>task);
    }
    [Fact] public async Task NumericEnumValuesAreAcceptedWhenListed()
    {
        var help=Help.Replace(" -preset <int> E..V....... speed (from -2 to 8) (default -2)"," -preset <int> E..V....... speed (from 0 to 8)\n    fast 6 E..V.......\n    slow 2 E..V.......");
        await new FfmpegEncoderProbe(new Runner(help)).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1,Preset="6"});
        await Assert.ThrowsAsync<ArgumentException>(()=>new FfmpegEncoderProbe(new Runner(help)).ValidateAsync("fixture",new(){Encoder=VideoEncoder.Libsvtav1,Preset="8"}));
    }
    private sealed class Runner(string help,bool stderr=false):ICommandRunner
    {
        public IReadOnlyList<string>? Arguments;public Action? OnQuery;
        public Task<ToolResult> RunAsync(string path,IReadOnlyList<string> args,CancellationToken token)
        {Arguments=args;OnQuery?.Invoke();return Task.FromResult(new ToolResult(0,stderr?"":help,stderr?help:""));}
    }
}
