using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Playback;

namespace ShareVibeX.Core.Tests;

public sealed class VideoEditTimelineTests
{
    [Fact]
    public void ChainedEditsMapCurrentFramesBackToImmutableSource()
    {
        var original=new VideoEditTimeline(300,30);var removed=original.Remove(60,120);
        Assert.Equal(240,removed.FrameCount);Assert.Equal(new[]{new VideoFrameSpan(0,60),new VideoFrameSpan(120,300)},removed.Spans);
        var kept=removed.Keep(30,90);Assert.Equal(new[]{new VideoFrameSpan(30,60),new VideoFrameSpan(120,150)},kept.Spans);
        Assert.Equal(300,original.FrameCount);Assert.Equal(original.Fingerprint,kept.Reset().Fingerprint);
        Assert.Equal(60,kept.FrameCount);Assert.Equal(TimeSpan.FromSeconds(2),kept.Duration);
    }
    [Theory]
    [InlineData(0,0)] [InlineData(-1,10)] [InlineData(20,10)] [InlineData(0,301)]
    public void InvalidIntervalsAreRejected(long a,long b)=>Assert.ThrowsAny<ArgumentException>(()=>new VideoEditTimeline(300,30).Keep(a,b));
    [Fact]
    public void RemovingEverythingIsRejectedAndMarkersSnapToSourceFrames()
    {
        var timeline=new VideoEditTimeline(300,30);Assert.Throws<ArgumentException>(()=>timeline.Remove(0,300));
        Assert.Equal(16,timeline.Snap(TimeSpan.FromSeconds(.52)));Assert.Equal(300,timeline.Snap(TimeSpan.FromSeconds(99)));
        Assert.Equal(timeline.Fingerprint,timeline.Keep(0,300).Fingerprint);
    }
    [Fact]
    public void EditCommandSplitsAndTrimsVideoAndAudioOnTheSameFrameGrid()
    {
        var timeline=new VideoEditTimeline(300,30).Remove(60,120);
        var args=VideoEditCommand.Build(new("clip.mp4",null,TimeSpan.FromSeconds(10),100,100,30,true),timeline,new(),"out.mp4");
        var graph=args[args.IndexOf("-filter_complex")+1];
        Assert.Contains("split=2",graph);Assert.Contains("trim=start_frame=120:end_frame=300",graph);Assert.Contains("atrim=start=4:end=10",graph);
        Assert.Contains("concat=n=2:v=1:a=1",graph);Assert.Contains("libx264",args);Assert.Equal("out.mp4",args.Last());
    }
}
