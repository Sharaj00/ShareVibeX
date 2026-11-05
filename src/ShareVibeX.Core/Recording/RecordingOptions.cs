using System.Text.Json.Serialization;

namespace ShareVibeX.Core.Recording;

public enum VideoContainer { Mp4, Matroska, Webm, Gif, AnimatedWebp, Apng }
public enum VideoEncoder { Libx264, Libx265, H264Nvenc, HevcNvenc, H264Qsv, H264Amf, HevcQsv, HevcAmf, Libvpx, LibvpxVp9, Libsvtav1, LibaomAv1, Libxvid, Gif, LibwebpAnim, Apng, Ffv1 }
public enum VideoRateControl { Quality, Bitrate }
public enum AudioSource { System, Microphone, SystemAndMicrophone, DirectShow }
public enum AudioEncoder { Aac, Libopus }
public enum ScaleMode { Original, Percentage, DivideBy, TargetSize }
public enum ScaleAlgorithm { Bilinear, Bicubic, Lanczos }

public sealed record RecordingOptions
{
    public IReadOnlyDictionary<VideoEncoder,VideoEncoderProfile>? EncoderProfiles {get;init;}
    public RecordingOptions SwitchEncoder(VideoEncoder encoder)
    {
        if(!Enum.IsDefined(encoder))throw new ArgumentOutOfRangeException(nameof(encoder));
        if(encoder==Encoder)return this;
        var profiles=EncoderProfiles is null?new Dictionary<VideoEncoder,VideoEncoderProfile>():new(EncoderProfiles);
        profiles[Encoder]=VideoEncoderProfile.From(this);
        var profile=profiles.GetValueOrDefault(encoder)??VideoEncoderProfile.From(new());
        var container=RecordingValidation.Containers(encoder).Contains(Container)?Container:RecordingValidation.Containers(encoder)[0];
        return this with{GifColors=profile.GifColors,GifDither=profile.GifDither,AnimationLoopCount=profile.AnimationLoopCount,AnimationLossless=profile.AnimationLossless,AnimationCompressionLevel=profile.AnimationCompressionLevel,UseCustomCommand=encoder is VideoEncoder.Gif or VideoEncoder.LibwebpAnim or VideoEncoder.Apng?false:UseCustomCommand,Container=container,AudioEncoder=container==VideoContainer.Webm?AudioEncoder.Libopus:container==VideoContainer.Mp4?AudioEncoder.Aac:AudioEncoder,AudioSampleRate=container==VideoContainer.Webm?48000:AudioSampleRate,Encoder=encoder,EncoderProfiles=profiles,RateControl=encoder is VideoEncoder.Gif or VideoEncoder.LibwebpAnim or VideoEncoder.Apng?VideoRateControl.Quality:profile.RateControl,Quality=profile.Quality,
            VideoBitrateKbps=profile.BitrateKbps,Preset=profile.Preset,Tune=profile.Tune,AdditionalArguments=profile.AdditionalArguments};
    }
    public RecordingOptions CaptureOptions()=>!IsAnimation?this:this with{Encoder=VideoEncoder.Ffv1,Container=VideoContainer.Matroska,EncoderProfiles=null,UseCustomCommand=false,RateControl=VideoRateControl.Quality,Quality=23,Preset="auto",Tune="",AdditionalArguments="",AnimationCompressionLevel=4};
    public int GifColors {get;init;}=256;
    public GifDither GifDither {get;init;}
    public int AnimationLoopCount {get;init;}
    public bool AnimationLossless {get;init;}
    public int AnimationCompressionLevel {get;init;}=4;
    [JsonIgnore] public bool IsAnimation=>Encoder is VideoEncoder.Gif or VideoEncoder.LibwebpAnim or VideoEncoder.Apng;
    public bool ShowVideoTimings {get;init;}=true;
    public string? FfmpegPath { get; init; }
    public string? OutputDirectory { get; init; }
    public VideoContainer Container { get; init; } = VideoContainer.Mp4;
    public VideoEncoder Encoder { get; init; } = VideoEncoder.Libx264;
    public int FrameRate { get; init; } = 30;
    public VideoRateControl RateControl { get; init; } = VideoRateControl.Quality;
    public int Quality { get; init; } = 23;
    public int VideoBitrateKbps { get; init; } = 8000;
    public string Preset { get; init; } = "auto";
    public string Tune { get; init; } = "";
    public bool IncludeCursor { get; init; } = true;
    public double DurationSeconds { get; init; }
    public AudioSource AudioSource { get; init; } = AudioSource.System;
    public string? PlaybackDeviceId { get; init; }
    public string? MicrophoneDeviceId { get; init; }
    public string DirectShowDevice { get; init; } = "";
    public AudioEncoder AudioEncoder { get; init; } = AudioEncoder.Aac;
    public int AudioBitrateKbps { get; init; } = 128;
    public int AudioSampleRate { get; init; } = 48000;
    public int AudioChannels { get; init; } = 2;
    public ScaleMode ScaleMode { get; init; }
    public double ScalePercent { get; init; } = 100;
    public double DivideBy { get; init; } = 1.5;
    public int TargetWidth { get; init; } = 1280;
    public int TargetHeight { get; init; }
    public bool KeepAspectRatio { get; init; } = true;
    public ScaleAlgorithm ScaleAlgorithm { get; init; } = ScaleAlgorithm.Lanczos;
    public string AdditionalArguments { get; init; } = "";
    public bool UseCustomCommand { get; init; }
    public string CustomCommand { get; init; } = DefaultTemplate;

    public const string DefaultTemplate = "-f gdigrab -framerate $fps$ -offset_x $area_x$ -offset_y $area_y$ -video_size $area_width$x$area_height$ -draw_mouse $cursor$ -i desktop $audio_input$ -map 0:v:0 -c:v libx264 -preset veryfast -crf 23 -vf $scale_filter$ $audio_options$ $output$";
    [JsonIgnore] public string EncoderName => Encoder switch
    {
        VideoEncoder.Libx264 => "libx264", VideoEncoder.Libx265 => "libx265",
        VideoEncoder.H264Nvenc => "h264_nvenc", VideoEncoder.HevcNvenc => "hevc_nvenc",
        VideoEncoder.H264Qsv => "h264_qsv", VideoEncoder.H264Amf => "h264_amf",
        VideoEncoder.HevcQsv=>"hevc_qsv",VideoEncoder.HevcAmf=>"hevc_amf",
        VideoEncoder.Libvpx=>"libvpx",VideoEncoder.LibvpxVp9=>"libvpx-vp9",
        VideoEncoder.Libsvtav1=>"libsvtav1",VideoEncoder.LibaomAv1=>"libaom-av1",VideoEncoder.Libxvid=>"libxvid",VideoEncoder.Gif=>"gif",VideoEncoder.LibwebpAnim=>"libwebp_anim",VideoEncoder.Apng=>"apng",VideoEncoder.Ffv1=>"ffv1",
        _ => throw new ArgumentOutOfRangeException(nameof(Encoder))
    };
    [JsonIgnore] public string Extension => Container switch{VideoContainer.Mp4=>"mp4",VideoContainer.Matroska=>"mkv",VideoContainer.Webm=>"webm",VideoContainer.Gif=>"gif",VideoContainer.AnimatedWebp=>"webp",VideoContainer.Apng=>"apng",_=>throw new ArgumentOutOfRangeException(nameof(Container))};
}

public enum GifDither { Sierra, Bayer, None }

public sealed record VideoEncoderProfile(VideoRateControl RateControl,int Quality,int BitrateKbps,string Preset,string Tune,string AdditionalArguments,int AnimationLoopCount=0,bool AnimationLossless=false,int AnimationCompressionLevel=4,int GifColors=256,GifDither GifDither=GifDither.Sierra)
{
    public static VideoEncoderProfile From(RecordingOptions options)=>new(options.RateControl,options.Quality,options.VideoBitrateKbps,options.Preset,options.Tune,options.AdditionalArguments,options.AnimationLoopCount,options.AnimationLossless,options.AnimationCompressionLevel,options.GifColors,options.GifDither);
}

