using ShareVibeX.Core.Capture;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ShareVibeX.Core.Recording;

public sealed record FfmpegCommand(IReadOnlyList<string> Arguments, int Width, int Height);

public static class FfmpegCommandBuilder
{
    private static readonly HashSet<string> ExtraOptions = ["-g", "-keyint_min", "-bf", "-refs", "-maxrate", "-bufsize", "-profile:v", "-level:v", "-threads", "-metadata"];
    internal static string GifPalette(RecordingOptions options)
    {
        var dither = options.GifDither switch
        {
            GifDither.Bayer => "bayer",
            GifDither.None => "none",
            _ => "sierra2_4a"
        };
        return $"split[gifbase][gifsource];[gifsource]palettegen=stats_mode=single:max_colors={N(options.GifColors)}[gifpalette];[gifbase][gifpalette]paletteuse=new=1:diff_mode=rectangle:dither={dither}";
    }

    private static readonly HashSet<string> CustomOptions = [..ExtraOptions, "-f", "-i", "-framerate", "-offset_x", "-offset_y", "-video_size", "-draw_mouse", "-thread_queue_size", "-rtbufsize", "-probesize", "-analyzeduration", "-c:v", "-c:a", "-b:v", "-b:a", "-crf", "-cq", "-global_quality", "-rc", "-qp_i", "-qp_p", "-preset", "-tune", "-quality", "-ar", "-ac", "-map", "-vf", "-af", "-pix_fmt", "-movflags", "-t"];
    private static readonly HashSet<string> Formats = ["gdigrab", "dshow", "f32le", "s16le", "mp4", "matroska", "webm"];
    public static FfmpegCommand Build(RecordingOptions options, PixelRect region, bool withAudio,
        string output, IReadOnlyList<string>? audioInput = null,WgcVideoSource? wgcSource=null,IReadOnlyList<string>? videoInput=null)
    {
        RecordingValidation.Validate(options);
        if(videoInput is not null&&(options.UseCustomCommand||wgcSource is not null))
            throw new ArgumentException("A managed video input cannot be combined with another input source.",nameof(videoInput));
        if(options.IsAnimation&&withAudio)throw new ArgumentException(Loc.T("Animations cannot contain an audio track."));
        if(wgcSource is not null&&(options.UseCustomCommand||wgcSource.Region!=region||wgcSource.FrameRate!=options.FrameRate||wgcSource.IncludeCursor!=options.IncludeCursor))
            throw new ArgumentException(Loc.T("The managed WGC source must match the recording region and cannot replace a custom command."));
        if (string.IsNullOrWhiteSpace(output) || output.Contains('\0')) throw new ArgumentException(Loc.T("A managed output path is required."));
        var size = VideoScaling.Calculate(region, options);
        if (withAudio && (audioInput is null || audioInput.Count == 0)) throw new ArgumentException(Loc.T("An explicit audio input is required."));
        if(withAudio && options.Container==VideoContainer.Webm && (options.AudioEncoder!=AudioEncoder.Libopus||options.AudioSampleRate!=48000))throw new ArgumentException(Loc.T("Use Opus at 48000 Hz for WebM audio."));
        if (withAudio && options.Container == VideoContainer.Mp4 && options.AudioEncoder == AudioEncoder.Libopus)
            throw new ArgumentException(Loc.T("Use AAC for MP4 compatibility, or choose Matroska for Opus."));
        if(withAudio && options.AudioEncoder==AudioEncoder.Libopus && options.AudioSampleRate!=48000)
            throw new ArgumentException(Loc.T("Select 48000 Hz output for Opus audio."));
        var audio = withAudio ? new List<string> { "-map", "1:a:0", "-c:a", options.AudioEncoder == AudioEncoder.Aac ? "aac" : "libopus",
            "-b:a", $"{options.AudioBitrateKbps}k", "-ar", N(options.AudioSampleRate), "-ac", N(options.AudioChannels) } : ["-an"];
        List<string> arguments = ["-hide_banner", "-y"];
        if (options.UseCustomCommand)
            arguments.AddRange(Custom(options, region, withAudio, output, audioInput ?? [], audio, size.Filter));
        else
        {
            if(videoInput is not null)arguments.AddRange(videoInput);
            else if(wgcSource is not null)arguments.AddRange(["-thread_queue_size","1024","-f","lavfi","-i",wgcSource.InputGraph]);
            else arguments.AddRange(["-thread_queue_size", "1024", "-f", "gdigrab", "-framerate", N(options.FrameRate),
                "-offset_x", N(region.X), "-offset_y", N(region.Y), "-video_size", $"{region.Width}x{region.Height}",
                "-draw_mouse", options.IncludeCursor ? "1" : "0", "-i", "desktop"]);
            if (withAudio) arguments.AddRange(audioInput!);
            arguments.AddRange(["-map", "0:v:0", "-c:v", options.EncoderName]);
            AddEncoding(arguments, options);
            arguments.AddRange(["-vf", options.Encoder==VideoEncoder.Gif ? size.Filter+","+GifPalette(options) : size.Filter]);
            arguments.AddRange(audio);
            AddAdditionalEncoding(arguments,options);
            if (options.DurationSeconds > 0) arguments.AddRange(["-t", N(options.DurationSeconds)]);
            AddContainer(arguments, options); arguments.Add(output);
        }
        return new(arguments.AsReadOnly(), size.Width, size.Height);
    }

    internal static void AddAdditionalEncoding(List<string> args,RecordingOptions options)
    {
        var extra=FfmpegArguments.Parse(options.AdditionalArguments);ValidatePairs(extra,ExtraOptions,false);args.AddRange(extra);
    }
    internal static void AddEncoding(List<string> args, RecordingOptions value)
    {
        if(value.Encoder==VideoEncoder.Ffv1){args.AddRange(["-level","3","-pix_fmt","bgr0"]);return;}
        if(value.IsAnimation)
        {
            if(value.Encoder==VideoEncoder.LibwebpAnim)
                args.AddRange(["-lossless",value.AnimationLossless?"1":"0","-quality",N(value.Quality),"-compression_level",N(value.AnimationCompressionLevel),"-pix_fmt",value.AnimationLossless?"bgra":"yuv420p"]);
            else if(value.Encoder==VideoEncoder.Apng)args.AddRange(["-compression_level",N(value.AnimationCompressionLevel),"-pix_fmt","rgba"]);
            else args.AddRange(["-pix_fmt","pal8"]);
            return;
        }
        if(value.Encoder is VideoEncoder.Libvpx or VideoEncoder.LibvpxVp9 or VideoEncoder.LibaomAv1 or VideoEncoder.Libsvtav1 or VideoEncoder.Libxvid)
        {
            if(value.Encoder!=VideoEncoder.Libxvid)
                args.AddRange([value.Encoder==VideoEncoder.Libsvtav1?"-preset":"-cpu-used",value.Preset=="auto"?"6":value.Preset]);
            if(value.RateControl==VideoRateControl.Bitrate)args.AddRange(["-b:v",$"{value.VideoBitrateKbps}k"]);
            else if(value.Encoder==VideoEncoder.Libxvid)args.AddRange(["-q:v",N(value.Quality)]);
            else
            {
                args.AddRange(["-crf",N(value.Quality)]);
                if(value.Encoder!=VideoEncoder.Libsvtav1)args.AddRange(["-b:v",value.Encoder==VideoEncoder.Libvpx?$"{value.VideoBitrateKbps}k":"0"]);
            }
            return;
        }
        var preset = value.Preset == "auto" ? value.Encoder switch
        { VideoEncoder.H264Nvenc or VideoEncoder.HevcNvenc => "p4", VideoEncoder.H264Amf or VideoEncoder.HevcAmf => "balanced", _ => "veryfast" } : value.Preset;
        args.AddRange([value.Encoder is VideoEncoder.H264Amf or VideoEncoder.HevcAmf ? "-quality" : "-preset", preset]);
        if (value.Tune.Length > 0) args.AddRange(["-tune", value.Tune]);
        if (value.RateControl == VideoRateControl.Bitrate) args.AddRange(["-b:v", $"{value.VideoBitrateKbps}k"]);
        else switch (value.Encoder)
        {
            case VideoEncoder.H264Nvenc: case VideoEncoder.HevcNvenc:
                args.AddRange(["-rc", "vbr", "-cq", N(value.Quality), "-b:v", "0"]); break;
            case VideoEncoder.H264Qsv: case VideoEncoder.HevcQsv: args.AddRange(["-global_quality", N(value.Quality)]); break;
            case VideoEncoder.H264Amf: case VideoEncoder.HevcAmf: args.AddRange(["-rc", "cqp", "-qp_i", N(value.Quality), "-qp_p", N(value.Quality)]); break;
            default: args.AddRange(["-crf", N(value.Quality)]); break;
        }
    }

    private static IReadOnlyList<string> Custom(RecordingOptions options, PixelRect region, bool withAudio, string output,
        IReadOnlyList<string> audioInput, IReadOnlyList<string> audioOptions, string scale)
    {
        var tokens = FfmpegArguments.Parse(options.CustomCommand);
        if (tokens.Count == 0 || tokens.Last() != "$output$" || tokens.Count(t => t == "$output$") != 1)
            throw new ArgumentException(Loc.T("Custom commands must end with exactly one standalone $output$ destination."));
        if (options.ScaleMode != ScaleMode.Original && !tokens.Where((t, i) => i > 0 && tokens[i - 1] == "-vf").Any(t => t.Contains("$scale_filter$", StringComparison.Ordinal)))
            throw new ArgumentException(Loc.T("Include $scale_filter$ to apply the selected scaling in custom mode."));
        if (withAudio && tokens.Count(t => t == "$audio_input$") != 1) throw new ArgumentException(Loc.T("Include exactly one standalone $audio_input$ for the selected audio source."));
        var values = new Dictionary<string, string>
        {
            ["fps"] = N(options.FrameRate), ["area_x"] = N(region.X), ["area_y"] = N(region.Y),
            ["area_width"] = N(region.Width), ["area_height"] = N(region.Height),
            ["cursor"] = options.IncludeCursor ? "1" : "0", ["scale_filter"] = scale,
        };
        if (options.DurationSeconds > 0) values["duration"] = N(options.DurationSeconds);
        var args = new List<string>();
        foreach (var token in tokens.SkipLast(1))
        {
            if (token == "$audio_input$")
            {
                if (withAudio)
                {
                    if (args.Count(t => t == "-i") != 1) throw new ArgumentException(Loc.T("Place $audio_input$ after the single video input in audio mode."));
                    args.AddRange(audioInput);
                }
                continue;
            }
            if (token == "$audio_options$") { args.AddRange(audioOptions); continue; }
            if (token.Contains("$output$", StringComparison.Ordinal)) throw new ArgumentException(Loc.T("$output$ is only valid as the final output destination."));
            var expanded = Regex.Replace(token, @"\$([a-zA-Z_]+)\$", match => values.TryGetValue(match.Groups[1].Value, out var value) ? value :
                throw new ArgumentException(Loc.F("Unknown or unavailable placeholder: {0}. $duration$ requires a positive time limit.",match.Value)), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            // Dollar signs left in the template indicate malformed placeholders, not expanded path values.
            if (Regex.Replace(token, @"\$([a-zA-Z_]+)\$", "", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Contains('$'))
                throw new ArgumentException(Loc.T("Malformed custom-command placeholder."));
            args.Add(expanded);
        }
        ValidatePairs(args, CustomOptions, true);
        var lastInput = args.FindLastIndex(t => t == "-i");
        var disableAudio = args.FindLastIndex(t => t == "-an");
        if (lastInput < 0 || !withAudio && (disableAudio < lastInput || args.Contains("-c:a") || args.Contains("-b:a") || args.Contains("-af")))
            throw new ArgumentException(Loc.T("Silent custom commands must disable output audio with -an after the last input and omit audio encoding options."));
        if (withAudio && (disableAudio >= 0 || args.FindLastIndex(t => t == "-c:a") < lastInput ||
            args.Count(t => t == "-i") != 2 || !args.Where((t, i) => i > lastInput && i > 0 && args[i - 1] == "-map").Contains("1:a:0")))
            throw new ArgumentException(Loc.T("Audio custom commands require one video input followed by $audio_input$, output -map 1:a:0 and -c:a options (or $audio_options$), and no -an."));
        if (options.DurationSeconds > 0)
        {
            var durationIndex = args.FindIndex(t => t == "-t");
            if (args.Count(t => t == "-t") != 1 || durationIndex < lastInput || args[durationIndex + 1] != N(options.DurationSeconds) ||
                !tokens.Where((t, i) => i > 0 && tokens[i - 1] == "-t").Contains("$duration$"))
                throw new ArgumentException(Loc.T("Use exactly one output -t $duration$ after the last input to apply the selected time limit."));
        }
        if (options.ScaleMode != ScaleMode.Original && args.FindIndex(t => t == "-vf") < lastInput)
            throw new ArgumentException(Loc.T("Put -vf $scale_filter$ after the last input to apply output scaling."));
        if (args.Count(t => t == "-vf") > 1) throw new ArgumentException(Loc.T("Use one ordered -vf filter pipeline."));
        AddContainer(args, options); args.Add(output); return args;
    }

    private static void ValidatePairs(IReadOnlyList<string> args, HashSet<string> allowed, bool custom)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var key = args[i];
            if (custom && key == "-an") continue;
            if (!allowed.Contains(key) || ++i >= args.Count || args[i].Length == 0 || args[i].Contains('\0') ||
                args[i].StartsWith('-') && !double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                throw new ArgumentException(Loc.F("Unsupported or incomplete option: {0}. Managed input, filters and output cannot be overridden in additional arguments.",key));
            if (key == "-f" && !Formats.Contains(args[i])) throw new ArgumentException(Loc.T("Unsupported custom input/container format; multiple-output muxers are not allowed."));
        }
    }
    internal static void AddContainer(List<string> args, RecordingOptions options)
    {
        args.AddRange(["-f", options.Container switch{VideoContainer.Mp4=>"mp4",VideoContainer.Matroska=>"matroska",VideoContainer.Webm=>"webm",VideoContainer.Gif=>"gif",VideoContainer.AnimatedWebp=>"webp",VideoContainer.Apng=>"apng",_=>throw new ArgumentOutOfRangeException(nameof(options.Container))}]);
        if(options.IsAnimation)args.AddRange([options.Encoder==VideoEncoder.Apng?"-plays":"-loop",N(options.AnimationLoopCount)]);
        if (options.Container == VideoContainer.Mp4) args.AddRange(["-movflags", "+faststart"]);
    }
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string N(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
}
