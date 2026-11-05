using System.Text.RegularExpressions;

namespace ShareVibeX.Core.Recording;

public sealed class FfmpegAnimationProbe(ICommandRunner runner)
{
    public async Task ValidateAsync(string path,RecordingOptions options,CancellationToken token=default)
    {
        if(!options.IsAnimation)throw new ArgumentException(Loc.T("An animation encoder is required."));
        var muxer=options.Encoder switch{VideoEncoder.Gif=>"gif",VideoEncoder.Apng=>"apng",_=>"webp"};
        var decoder=options.Encoder==VideoEncoder.LibwebpAnim?"webp_anim":muxer;
        async Task<HashSet<string>> Names(string option,string pattern)
        {
            token.ThrowIfCancellationRequested();var result=await runner.RunAsync(path,["-hide_banner",option],token);
            token.ThrowIfCancellationRequested();
            if(result.ExitCode!=0)throw new IOException(Loc.F("FFmpeg {0} failed (exit {1}): {2}",option,result.ExitCode,result.StandardError[..Math.Min(1000,result.StandardError.Length)]));
            return Regex.Matches(result.StandardOutput+"\n"+result.StandardError,pattern,RegexOptions.Multiline|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(200))
                .SelectMany(m=>m.Groups[1].Value.Split(',')).ToHashSet(StringComparer.Ordinal);
        }
        var muxers=await Names("-muxers",@"^[ \t]*E[ \t]+(\S+)[ \t]+");
        var demuxers=await Names("-demuxers",@"^[ \t]*D[ \t]+(\S+)[ \t]+");
        var decoders=await Names("-decoders",@"^[ \t]*V[A-Z.]{5}[ \t]+(\S+)[ \t]+");
        var missing=new List<string>();
        if(!muxers.Contains("matroska"))missing.Add("muxer=matroska");
        if(!demuxers.Contains("matroska"))missing.Add("demuxer=matroska");
        if(!muxers.Contains(muxer))missing.Add("muxer="+muxer);
        if(!demuxers.Contains(decoder))missing.Add("demuxer="+decoder);
        if(!decoders.Contains(decoder))missing.Add("decoder="+decoder);
        if(!decoders.Contains("ffv1"))missing.Add("decoder=ffv1");
        if(options.Encoder==VideoEncoder.Gif)
        {
            var filters=await Names("-filters",@"^[ \t]*[TSC.]{2,3}[ \t]+(\S+)[ \t]+");
            foreach(var filter in new[]{"split","palettegen","paletteuse"})
                if(!filters.Contains(filter))missing.Add("filter="+filter);
        }
        if(missing.Count>0)throw new NotSupportedException(Loc.F("The selected FFmpeg cannot preview/edit this animation: {0}. Choose another FFmpeg build.",string.Join(", ",missing)));
    }
}
