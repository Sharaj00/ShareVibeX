using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Capture;
namespace ShareVibeX.Core.Tests;
public sealed class ExpandedEncoderTests
{
    [Theory][InlineData(VideoEncoder.Libvpx)][InlineData(VideoEncoder.LibvpxVp9)]
    public void VpContainersAreRestrictedAndWebmUsesTheRightMuxer(VideoEncoder encoder)
    {
        Assert.Equal(new[]{VideoContainer.Matroska,VideoContainer.Webm},RecordingValidation.Containers(encoder));
        var switched=new RecordingOptions().SwitchEncoder(encoder);Assert.Equal(VideoContainer.Matroska,switched.Container);
        var options=switched with{Container=VideoContainer.Webm,AudioEncoder=AudioEncoder.Libopus};
        Assert.Equal("webm",options.Extension);
        var args=FfmpegCommandBuilder.Build(options,new(0,0,100,100),true,"clip.webm",["-f","f32le","-i","audio"]).Arguments;
        Assert.Contains("webm",args);Assert.Contains("libopus",args);Assert.DoesNotContain("+faststart",args);
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(options with{Container=VideoContainer.Mp4}));
        Assert.Throws<ArgumentException>(()=>FfmpegCommandBuilder.Build(options with{AudioEncoder=AudioEncoder.Aac},new(0,0,100,100),true,"clip.webm",["-i","audio"]));
    }
    [Theory]
    [InlineData(VideoEncoder.HevcQsv,"hevc_qsv","-global_quality")]
    [InlineData(VideoEncoder.HevcAmf,"hevc_amf","-qp_i")]
    [InlineData(VideoEncoder.Libvpx,"libvpx","-crf")]
    [InlineData(VideoEncoder.LibvpxVp9,"libvpx-vp9","-crf")]
    [InlineData(VideoEncoder.Libsvtav1,"libsvtav1","-crf")]
    [InlineData(VideoEncoder.LibaomAv1,"libaom-av1","-crf")]
    [InlineData(VideoEncoder.Libxvid,"libxvid","-q:v")]
    public void QualityBitrateAndAssemblyUseApplicableOptions(VideoEncoder encoder,string name,string quality)
    {
        var options=new RecordingOptions{Encoder=encoder,Container=VideoContainer.Matroska};
        var command=FfmpegCommandBuilder.Build(options,new(0,0,100,100),false,"clip.mkv");
        Assert.Equal(name,options.EncoderName);Assert.Contains(name,command.Arguments);Assert.Contains(quality,command.Arguments);
        var bitrate=FfmpegCommandBuilder.Build(options with{RateControl=VideoRateControl.Bitrate},new(0,0,100,100),false,"clip.mkv");
        Assert.Contains("8000k",bitrate.Arguments);Assert.DoesNotContain(quality,bitrate.Arguments);
        var segments=new[]{new RecordingSegment("a",TimeSpan.FromSeconds(1),1,false,100,100),new RecordingSegment("b",TimeSpan.FromSeconds(1),1,false,100,100)};
        var assembled=RecordingAssemblyCommand.Build(options,segments,false,"joined.mkv");Assert.Contains(name,assembled);Assert.Contains(quality,assembled);
        if(encoder is VideoEncoder.Libvpx or VideoEncoder.LibvpxVp9 or VideoEncoder.LibaomAv1)
        {Assert.Contains("-cpu-used",command.Arguments);Assert.DoesNotContain("-preset",command.Arguments);}
        if(encoder==VideoEncoder.Libxvid)Assert.DoesNotContain("-preset",command.Arguments);
    }
    [Fact] public void ProfileQualityRangesAndContainerAreValidated()
    {
        RecordingValidation.Validate(new(){Encoder=VideoEncoder.LibvpxVp9,Container=VideoContainer.Matroska,Quality=63});
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(new(){Encoder=VideoEncoder.Libx264,Quality=63}));
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(new(){Encoder=VideoEncoder.Libxvid,Quality=0}));
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(new(){Encoder=VideoEncoder.Libvpx}));
    }
    [Fact] public void NumericEncoderIdsRemainCompatible()
    {
        Assert.Equal(5,(int)VideoEncoder.H264Amf);Assert.Equal(0,(int)VideoEncoder.Libx264);
    }
    [Fact] public void InactiveVp8ProfileDoesNotRestrictTheActiveCodecContainer()
    {
        var options=new RecordingOptions{Encoder=VideoEncoder.Libvpx,Container=VideoContainer.Matroska};
        var h264=options.SwitchEncoder(VideoEncoder.Libx264) with{Container=VideoContainer.Mp4};
        RecordingValidation.Validate(h264);
    }
}
