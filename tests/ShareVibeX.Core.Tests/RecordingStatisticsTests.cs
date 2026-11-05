using ShareVibeX.Core.Recording;
namespace ShareVibeX.Core.Tests;
public sealed class RecordingStatisticsTests
{
    [Fact]
    public void HeaderOnlyProgressUsesLabeledEstimateAndScaledPixelCount()
    {
        var progress=new RecordingProgress(TimeSpan.FromSeconds(5),48);
        var full=RecordingStatistics.Resolve(progress,48,new(),1920,1080,false);
        var half=RecordingStatistics.Resolve(progress,48,new(),960,540,false);
        Assert.True(full.IsEstimated);Assert.True(full.TotalBytes>0);Assert.Equal(full.AverageBitrateKbps/4,half.AverageBitrateKbps);
        var bitrate=RecordingStatistics.Resolve(progress,0,new(){RateControl=VideoRateControl.Bitrate,VideoBitrateKbps=4000},960,540,true);
        Assert.Equal(4128,bitrate.AverageBitrateKbps);
    }
    [Fact]
    public void GrowingFileAndRealProgressWinOverEstimates()
    {
        var value=RecordingStatistics.Resolve(new(TimeSpan.FromSeconds(2),48),1000000,new(),100,100,false);
        Assert.False(value.IsEstimated);Assert.Equal(1000000,value.TotalBytes);Assert.Equal(4000,value.AverageBitrateKbps);
        Assert.Null(RecordingStatistics.Resolve(RecordingProgress.Empty,0,new(),100,100,false).TotalBytes);
    }
}
