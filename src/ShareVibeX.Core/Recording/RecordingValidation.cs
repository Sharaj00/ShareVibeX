namespace ShareVibeX.Core.Recording;

public static class RecordingValidation
{
    public static VideoContainer[] Containers(VideoEncoder encoder)=>encoder switch
    {
        VideoEncoder.Ffv1=>[VideoContainer.Matroska],
        VideoEncoder.Gif=>[VideoContainer.Gif],VideoEncoder.LibwebpAnim=>[VideoContainer.AnimatedWebp],VideoEncoder.Apng=>[VideoContainer.Apng],
        VideoEncoder.Libvpx or VideoEncoder.LibvpxVp9=>[VideoContainer.Matroska,VideoContainer.Webm],
        VideoEncoder.Libsvtav1 or VideoEncoder.LibaomAv1=>[VideoContainer.Mp4,VideoContainer.Matroska,VideoContainer.Webm],
        _=>[VideoContainer.Mp4,VideoContainer.Matroska]
    };
    public static int QualityMaximum(VideoEncoder encoder)=>encoder==VideoEncoder.LibwebpAnim?100:encoder is VideoEncoder.Libvpx or VideoEncoder.LibvpxVp9 or VideoEncoder.Libsvtav1 or VideoEncoder.LibaomAv1?63:encoder==VideoEncoder.Libxvid?31:51;
    public static int QualityMinimum(VideoEncoder encoder)=>encoder==VideoEncoder.Libxvid?1:0;
    public static string[] Presets(VideoEncoder encoder) => encoder switch
    {
        VideoEncoder.H264Nvenc or VideoEncoder.HevcNvenc => ["auto", "p1", "p2", "p3", "p4", "p5", "p6", "p7"],
        VideoEncoder.H264Amf or VideoEncoder.HevcAmf => ["auto", "speed", "balanced", "quality"],
        VideoEncoder.H264Qsv or VideoEncoder.HevcQsv => ["auto", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
        VideoEncoder.Libvpx or VideoEncoder.LibvpxVp9 or VideoEncoder.LibaomAv1 => ["auto",..Enumerable.Range(0,9).Select(n=>n.ToString(System.Globalization.CultureInfo.InvariantCulture))],
        VideoEncoder.Libsvtav1 => ["auto",..Enumerable.Range(0,14).Select(n=>n.ToString(System.Globalization.CultureInfo.InvariantCulture))],
        VideoEncoder.Libxvid or VideoEncoder.Gif or VideoEncoder.LibwebpAnim or VideoEncoder.Apng or VideoEncoder.Ffv1 => ["auto"],
        _ => ["auto", "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow", "placebo"]
    };
    public static string AutoPreset(VideoEncoder encoder)=>encoder switch
    {
        VideoEncoder.H264Nvenc or VideoEncoder.HevcNvenc=>"p4",
        VideoEncoder.H264Amf or VideoEncoder.HevcAmf=>"balanced",
        VideoEncoder.Libvpx or VideoEncoder.LibvpxVp9 or VideoEncoder.LibaomAv1 or VideoEncoder.Libsvtav1=>"6",
        _=>"veryfast"
    };
    public static string[] Tunes(VideoEncoder encoder) => encoder switch
    {
        VideoEncoder.Libx264 => ["", "film", "animation", "grain", "stillimage", "fastdecode", "zerolatency"],
        VideoEncoder.Libx265 => ["", "psnr", "ssim", "grain", "zerolatency", "fastdecode", "animation"],
        VideoEncoder.H264Nvenc or VideoEncoder.HevcNvenc => ["", "hq", "ll", "ull", "lossless"],
        _ => [""]
    };
    public static void Validate(RecordingOptions value)
    {
        if(value.Encoder==VideoEncoder.Gif && (value.GifColors is <4 or >256 || !Enum.IsDefined(value.GifDither)))
            throw new ArgumentException(Loc.T("Invalid GIF palette settings."));
        if(value.AnimationLoopCount is <0 or >65535 || value.AnimationCompressionLevel<0 || value.AnimationCompressionLevel>(value.Encoder==VideoEncoder.Apng?9:6))throw new ArgumentException(Loc.T("Invalid animation loop count or compression level."));
        if(value.IsAnimation&&(value.UseCustomCommand||value.RateControl!=VideoRateControl.Quality))throw new ArgumentException(Loc.T("Animations require structured quality settings without an audio track."));
        if(!Containers(value.Encoder).Contains(value.Container))
            throw new ArgumentException(Loc.T("Select a container supported by the chosen encoder."));
        if(value.EncoderProfiles is {} profiles)
        {
            if(profiles.Count>Enum.GetValues<VideoEncoder>().Length)throw new ArgumentException(Loc.T("Invalid saved encoder profiles."));
            foreach(var (encoder,profile) in profiles)
            {
                if(profile is null)throw new ArgumentException(Loc.T("Invalid saved encoder profiles."));
                Validate(value with{EncoderProfiles=null,UseCustomCommand=false,GifColors=profile.GifColors,GifDither=profile.GifDither,AnimationLoopCount=profile.AnimationLoopCount,AnimationLossless=profile.AnimationLossless,AnimationCompressionLevel=profile.AnimationCompressionLevel,Encoder=encoder,Container=Containers(encoder).Contains(VideoContainer.Matroska)?VideoContainer.Matroska:Containers(encoder)[0],RateControl=profile.RateControl,Quality=profile.Quality,
                    VideoBitrateKbps=profile.BitrateKbps,Preset=profile.Preset,Tune=profile.Tune,AdditionalArguments=profile.AdditionalArguments});
            }
        }
        if (!Enum.IsDefined(value.Container) || !Enum.IsDefined(value.Encoder) || !Enum.IsDefined(value.RateControl) ||
            !Enum.IsDefined(value.AudioSource) || !Enum.IsDefined(value.AudioEncoder) || !Enum.IsDefined(value.ScaleMode) || !Enum.IsDefined(value.ScaleAlgorithm))
            throw new ArgumentException(Loc.T("Unknown recording option."));
        if((value.Quality < QualityMinimum(value.Encoder) || value.Quality > QualityMaximum(value.Encoder)))throw new ArgumentException(Loc.F("Use quality {0}–{1} for the selected encoder.",QualityMinimum(value.Encoder),QualityMaximum(value.Encoder)));
        if (value.FrameRate is < 1 or > 240 || value.VideoBitrateKbps is < 100 or > 500000 ||
            !double.IsFinite(value.DurationSeconds) || value.DurationSeconds is < 0 or > 86400)
            throw new ArgumentException(Loc.T("Use 1–240 fps, quality 0–51, bitrate 100–500000 kbps and duration 0–86400 seconds."));
        if (!Presets(value.Encoder).Contains(value.Preset) || !Tunes(value.Encoder).Contains(value.Tune))
            throw new ArgumentException(Loc.T("Preset or tune is not supported by the selected encoder."));
        if (value.AudioBitrateKbps is < 32 or > 512 || value.AudioSampleRate is not (44100 or 48000) || value.AudioChannels is not (1 or 2))
            throw new ArgumentException(Loc.T("Use 32–512 kbps audio, 44100/48000 Hz and one or two channels."));
        if (value.AdditionalArguments is null || value.CustomCommand is null || value.DirectShowDevice is null)
            throw new ArgumentException(Loc.T("Recording text options must not be null."));
        if (!double.IsFinite(value.ScalePercent) || value.ScalePercent is < 0.01 or > 1600 ||
            !double.IsFinite(value.DivideBy) || value.DivideBy is < 0.01 or > 1000 ||
            value.TargetWidth is < 0 or > 16384 || value.TargetHeight is < 0 or > 16384)
            throw new ArgumentException(Loc.T("Use scaling 0.01–1600%, a divisor 0.01–1000 and target dimensions 0–16384."));
    }
}
