namespace ShareVibeX.Core.Recording;

public static class RecordingStatistics
{
    public static RecordingProgress Resolve(RecordingProgress progress,long? fileBytes,RecordingOptions options,int width,int height,bool audio)
    {
        var observed=Math.Max(progress.TotalBytes??0,fileBytes??0);
        if(observed>=4096)return progress with{TotalBytes=observed,IsEstimated=false};
        if(progress.Duration is not {TotalSeconds:>0} duration||width<=0||height<=0)return progress with{TotalBytes=null};
        // CRF size depends on content. This is a labeled display estimate, never export metadata.
        var video=options.RateControl==VideoRateControl.Bitrate?options.VideoBitrateKbps:
            Math.Clamp(width*(double)height*options.FrameRate*.10*Math.Pow(2,(23-options.Quality)/6d)/1000,1,500000);
        var kbps=video+(audio?options.AudioBitrateKbps:0);
        var estimate=(long)Math.Min(long.MaxValue,Math.Ceiling(duration.TotalSeconds*kbps*1000/8));
        return progress with{TotalBytes=Math.Max(observed,estimate),IsEstimated=true};
    }
}
