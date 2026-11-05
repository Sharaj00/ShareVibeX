using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Capture;
namespace ShareVibeX.Core.Tests;
public sealed class AnimationCommandTests
{
    [Theory][InlineData(VideoEncoder.Gif)][InlineData(VideoEncoder.LibwebpAnim)][InlineData(VideoEncoder.Apng)]
    public void AnimationEditDropsSourceAudioAndUsesAnimationEncoding(VideoEncoder encoder)
    {
        var options=new RecordingOptions().SwitchEncoder(encoder);
        var timeline=new ShareVibeX.Core.Editing.VideoEditTimeline(300,30).Remove(60,120);
        var args=ShareVibeX.Core.Editing.VideoEditCommand.Build(new("clip.mp4",null,TimeSpan.FromSeconds(10),100,100,30,true),timeline,options,"out."+options.Extension);
        var graph=args[args.IndexOf("-filter_complex")+1];
        if(encoder==VideoEncoder.Gif)Assert.Contains("paletteuse=new=1:diff_mode=rectangle:dither=sierra2_4a[gifout]",graph);Assert.DoesNotContain("atrim",graph);Assert.Contains("concat=n=2:v=1:a=0",graph);
        Assert.Contains("-an",args);Assert.Contains(options.EncoderName,args);Assert.DoesNotContain("-c:a",args);
    }
    [Theory]
    [InlineData(VideoEncoder.Gif,VideoContainer.Gif,"gif","gif","rgba","-loop")]
    [InlineData(VideoEncoder.LibwebpAnim,VideoContainer.AnimatedWebp,"libwebp_anim","webp","rgba","-loop")]
    [InlineData(VideoEncoder.Apng,VideoContainer.Apng,"apng","apng","rgba","-plays")]
    public void AnimationCommandsUseTheirOwnCodecContainerPixelsAndLoop(VideoEncoder encoder,VideoContainer container,string codec,string extension,string pixelFormat,string loop)
    {
        var options=new RecordingOptions().SwitchEncoder(encoder) with{AnimationLoopCount=3};
        Assert.Equal(container,options.Container);Assert.Equal(codec,options.EncoderName);Assert.Equal(extension,options.Extension);
        var command=FfmpegCommandBuilder.Build(options,new(0,0,101,99),false,"clip."+extension);
        Assert.Contains(codec,command.Arguments);Assert.Contains(loop,command.Arguments);Assert.Contains("3",command.Arguments);Assert.Contains("-an",command.Arguments);
        Assert.DoesNotContain("-crf",command.Arguments);Assert.DoesNotContain("-b:v",command.Arguments);Assert.DoesNotContain("-c:a",command.Arguments);Assert.DoesNotContain("-preset",command.Arguments);
        Assert.Contains("format="+pixelFormat,VideoScaling.Calculate(new(0,0,101,99),options).Filter);
        Assert.Throws<ArgumentException>(()=>FfmpegCommandBuilder.Build(options,new(0,0,100,100),true,"clip."+extension,["-i","audio"]));
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(options with{RateControl=VideoRateControl.Bitrate}));
        var segments=new[]{new RecordingSegment("a",TimeSpan.FromSeconds(1),1,false,102,100),new RecordingSegment("b",TimeSpan.FromSeconds(1),1,false,102,100)};
        var assembly=RecordingAssemblyCommand.Build(options,segments,false,"clip."+extension);
        if(encoder==VideoEncoder.Gif)
        {
            var gif = options with { GifColors=32, GifDither=GifDither.Bayer };
            Assert.Contains(RecordingAssemblyCommand.Build(gif,segments[..1],false,"clip.gif"),
                arg=>arg.Contains("max_colors=32")&&arg.Contains("dither=bayer"));
            Assert.Contains(assembly,arg=>arg.Contains("paletteuse=new=1:diff_mode=rectangle:dither=sierra2_4a[gifout]"));
            Assert.Equal(32,gif.SwitchEncoder(VideoEncoder.Apng).SwitchEncoder(VideoEncoder.Gif).GifColors);
        }
        Assert.Contains(codec,assembly);Assert.Contains(loop,assembly);Assert.DoesNotContain("-c:a",assembly);
    }
    [Fact] public void LosslessWebpAndApngCompressionHaveBoundedIndependentSettings()
    {
        var webp=new RecordingOptions().SwitchEncoder(VideoEncoder.LibwebpAnim) with{AnimationLossless=true,Quality=100};
        var args=FfmpegCommandBuilder.Build(webp,new(0,0,100,100),false,"clip.webp").Arguments;
        Assert.Contains("-lossless",args);Assert.Contains("bgra",args);Assert.Contains("100",args);
        var apng=webp.SwitchEncoder(VideoEncoder.Apng) with{AnimationCompressionLevel=9};RecordingValidation.Validate(apng);
        Assert.Equal(4,apng.SwitchEncoder(VideoEncoder.LibwebpAnim).AnimationCompressionLevel);
        Assert.True(apng.SwitchEncoder(VideoEncoder.LibwebpAnim).AnimationLossless);
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(webp with{AnimationLoopCount=65536}));
    }
}
