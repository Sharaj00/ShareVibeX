using System.Globalization;
using ShareVibeX.Core.Playback;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Core.Editing;

public static class VideoEditCommand
{
    public static List<string> Build(PreviewMedia source,VideoEditTimeline edit,RecordingOptions options,string output)
    {
        RecordingValidation.Validate(options);
        var hasAudio=source.HasAudio&&!options.IsAnimation;
        if(hasAudio&&options.Container==VideoContainer.Mp4&&options.AudioEncoder==AudioEncoder.Libopus)throw new ArgumentException(Loc.T("Use AAC for MP4 or choose MKV for Opus."));
        if(hasAudio&&options.Container==VideoContainer.Webm&&(options.AudioEncoder!=AudioEncoder.Libopus||options.AudioSampleRate!=48000))throw new ArgumentException(Loc.T("Use Opus at 48000 Hz for WebM audio."));
        List<string> args=["-hide_banner","-y","-i",source.Path];var filters=new List<string>();var n=edit.Spans.Count;
        if(n>1)
        {
            filters.Add("[0:v:0]split="+n+string.Concat(Enumerable.Range(0,n).Select(i=>$"[vs{i}]")));
            if(hasAudio)filters.Add("[0:a:0]asplit="+n+string.Concat(Enumerable.Range(0,n).Select(i=>$"[as{i}]")));
        }
        var inputs="";
        for(var i=0;i<n;i++)
        {
            var span=edit.Spans[i];var v=n==1?"[0:v:0]":$"[vs{i}]";var a=n==1?"[0:a:0]":$"[as{i}]";
            string Seconds(long frame)=>(frame/(double)edit.FrameRate).ToString("0.#########",CultureInfo.InvariantCulture);
            filters.Add(v+$"trim=start_frame={span.Start}:end_frame={span.End},setpts=PTS-STARTPTS[v{i}]");inputs+=$"[v{i}]";
            if(hasAudio){filters.Add(a+$"atrim=start={Seconds(span.Start)}:end={Seconds(span.End)},asetpts=PTS-STARTPTS,apad,atrim=duration={Seconds(span.Length)}[a{i}]");inputs+=$"[a{i}]";}
        }
        if(n>1)filters.Add(inputs+$"concat=n={n}:v=1:a={(hasAudio?1:0)}[v]"+(hasAudio?"[a]":""));
        var video = n==1?"[v0]":"[v]";
        if(options.Encoder==VideoEncoder.Gif)
        {
            filters.Add(video+FfmpegCommandBuilder.GifPalette(options)+"[gifout]");
            video="[gifout]";
        }
        args.AddRange(["-filter_complex",string.Join(';',filters),"-map",video,"-c:v",options.EncoderName]);
        FfmpegCommandBuilder.AddEncoding(args,options);FfmpegCommandBuilder.AddAdditionalEncoding(args,options);
        if(hasAudio)args.AddRange(["-map",n==1?"[a0]":"[a]","-c:a",options.AudioEncoder==AudioEncoder.Aac?"aac":"libopus","-b:a",options.AudioBitrateKbps+"k","-ar",options.AudioSampleRate.ToString(CultureInfo.InvariantCulture),"-ac",options.AudioChannels.ToString(CultureInfo.InvariantCulture)]);
        else args.Add("-an");
        args.AddRange(["-r",edit.FrameRate.ToString(CultureInfo.InvariantCulture)]);FfmpegCommandBuilder.AddContainer(args,options);args.Add(output);return args;
    }
}
