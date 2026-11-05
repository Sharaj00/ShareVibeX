using System.Globalization;

namespace ShareVibeX.Core.Recording;

public sealed record RecordingSegment(string Path,TimeSpan? Duration,long TotalBytes,bool HasAudio,int Width,int Height);

public static class RecordingAssemblyCommand
{
    public static List<string> Build(RecordingOptions options,IReadOnlyList<RecordingSegment> segments,bool keepAudio,string output)
    {
        if(options.IsAnimation&&keepAudio)throw new ArgumentException(Loc.T("Animations cannot contain an audio track."));
        if(segments.Count==0)throw new ArgumentException(Loc.T("No recording segments are available."));
        List<string> args=["-hide_banner","-y"];
        foreach(var segment in segments)args.AddRange(["-i",segment.Path]);
        if(segments.Count==1&&options.Encoder==VideoEncoder.Gif)
            args.AddRange(["-vf",FfmpegCommandBuilder.GifPalette(options)]);
        if(segments.Count==1&&options.IsAnimation)
        {
            args.AddRange(["-map","0:v:0","-c:v",options.EncoderName]);
            FfmpegCommandBuilder.AddEncoding(args,options);FfmpegCommandBuilder.AddAdditionalEncoding(args,options);args.Add("-an");
        }
        else if(segments.Count==1)
        {
            args.AddRange(["-map","0:v:0","-c:v","copy"]);
            args.AddRange(keepAudio?["-map","0:a:0","-c:a","copy"]:["-an"]);
        }
        else
        {
            if(options.UseCustomCommand)throw new NotSupportedException(Loc.T("Segment assembly requires structured encoding settings."));
            if(segments.Any(s=>s.Duration is null || s.Duration<=TimeSpan.Zero || s.Width!=segments[0].Width || s.Height!=segments[0].Height || keepAudio&&!s.HasAudio))
                throw new InvalidDataException(Loc.T("Segment duration, size or audio metadata is incompatible. Original segments were retained."));
            var filters=new List<string>();var inputs="";
            for(var i=0;i<segments.Count;i++)
            {
                var duration=segments[i].Duration!.Value.TotalSeconds.ToString("0.#########",CultureInfo.InvariantCulture);
                filters.Add($"[{i}:v:0]trim=duration={duration},setpts=PTS-STARTPTS[v{i}]");inputs+=$"[v{i}]";
                if(keepAudio){filters.Add($"[{i}:a:0]apad,atrim=duration={duration},asetpts=PTS-STARTPTS[a{i}]");inputs+=$"[a{i}]";}
            }
            filters.Add(inputs+$"concat=n={segments.Count}:v=1:a={(keepAudio?1:0)}[v]"+(keepAudio?"[a]":""));
            var video = "[v]";
            if(options.Encoder==VideoEncoder.Gif)
            {
                filters.Add(video+FfmpegCommandBuilder.GifPalette(options)+"[gifout]");
                video="[gifout]";
            }
            args.AddRange(["-filter_complex",string.Join(';',filters),"-map",video,"-c:v",options.EncoderName]);
            FfmpegCommandBuilder.AddEncoding(args,options);
            FfmpegCommandBuilder.AddAdditionalEncoding(args,options);
            if(keepAudio)args.AddRange(["-map","[a]","-c:a",options.AudioEncoder==AudioEncoder.Aac?"aac":"libopus","-b:a",options.AudioBitrateKbps+"k","-ar",options.AudioSampleRate.ToString(CultureInfo.InvariantCulture),"-ac",options.AudioChannels.ToString(CultureInfo.InvariantCulture)]);
            else args.Add("-an");
        }
        FfmpegCommandBuilder.AddContainer(args,options);args.Add(output);return args;
    }
}
