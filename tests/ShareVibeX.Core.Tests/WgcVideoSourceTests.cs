using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Core.Tests;

public sealed class WgcVideoSourceTests
{
    private static readonly MonitorInfo Left=new("a",new(-1920,-100,1920,1080),1.5);
    private static readonly MonitorInfo Right=new("b",new(100,0,1080,1920),1);
    [Fact] public void NegativeDesktopCoordinatesBecomeMonitorRelativeCrop()
    {
        var region=new PixelRect(-1800,0,501,301);
        var source=WgcVideoSource.Create(region,[new(Left,42)],60,false);
        Assert.Contains("hmonitor=42:capture_cursor=0:max_framerate=60:crop_left=120:crop_top=100:crop_right=1299:crop_bottom=679",source.InputGraph);
        Assert.Contains("width=501:height=301:resize_mode=crop:output_fmt=bgra,hwdownload,format=bgra",source.InputGraph);
        Assert.EndsWith("fps=60",source.InputGraph);Assert.DoesNotContain("monitor_idx",source.InputGraph);
    }
    [Fact] public void MultipleMonitorsPreserveBlackGapAndPhysicalOffsets()
    {
        var region=new PixelRect(-100,0,400,500);
        var source=WgcVideoSource.Create(region,[new(Right,99),new(Left,42)],30,true);
        Assert.Equal(2,source.MonitorCount);
        Assert.Contains("color=c=black:s=400x500:r=30,format=bgra[base0]",source.InputGraph);
        Assert.Contains("overlay=x=0:y=0:shortest=1:format=rgb",source.InputGraph);
        Assert.Contains("overlay=x=200:y=0:shortest=1:format=rgb",source.InputGraph);
        Assert.True(source.InputGraph.IndexOf("hmonitor=42",StringComparison.Ordinal)<source.InputGraph.IndexOf("hmonitor=99",StringComparison.Ordinal));
    }
    [Fact] public void CommandRetainsAudioMappingAndOutputScaling()
    {
        var region=new PixelRect(-100,0,101,99);var options=new RecordingOptions{FrameRate=60,ScaleMode=ScaleMode.Percentage,ScalePercent=50};
        var source=WgcVideoSource.Create(region,[new(Left,42),new(Right,99)],60,true);
        var command=FfmpegCommandBuilder.Build(options,region,true,"clip.mp4",["-f","f32le","-i","audio"],source);
        var args=command.Arguments.ToList();
        Assert.Equal("lavfi",args[args.IndexOf("-f")+1]);Assert.Contains(source.InputGraph,args);
        Assert.Contains("1:a:0",args);Assert.Contains("aac",args);Assert.Equal("clip.mp4",args.Last());
        Assert.Equal(52,command.Width);Assert.Equal(50,command.Height);Assert.DoesNotContain("gdigrab",args);
        Assert.Contains(VideoScaling.Calculate(region,options).Filter,args);
    }
    [Fact] public void EmptyHandleDuplicateAndNoIntersectAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>WgcVideoSource.Create(Left.Bounds,[new(Left,0)],30,true));
        Assert.Throws<InvalidDataException>(()=>WgcVideoSource.Create(Left.Bounds,[new(Left,42),new(Left,42)],30,true));
        Assert.Throws<CaptureSourceUnavailableException>(()=>WgcVideoSource.Create(new(5000,0,10,10),[new(Left,42)],30,true));
    }
    [Fact] public void StaleSourceCannotOverrideRegionCursorFpsOrCustomTemplate()
    {
        var source=WgcVideoSource.Create(Left.Bounds,[new(Left,42)],30,true);
        foreach(var options in new[]{new RecordingOptions{FrameRate=60},new RecordingOptions{IncludeCursor=false},new RecordingOptions{UseCustomCommand=true}})
            Assert.Throws<ArgumentException>(()=>FfmpegCommandBuilder.Build(options,Left.Bounds,false,"clip.mp4",wgcSource:source));
        Assert.Throws<ArgumentException>(()=>FfmpegCommandBuilder.Build(new(),new(0,0,10,10),false,"clip.mp4",wgcSource:source));
    }
}
