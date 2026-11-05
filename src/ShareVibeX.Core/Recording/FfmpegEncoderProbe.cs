using System.Globalization;
using System.Text.RegularExpressions;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Core.Recording;

public sealed class FfmpegEncoderProbe(ICommandRunner runner)
{
    public async Task ValidateAsync(string path,RecordingOptions options,CancellationToken token=default)
    {
        RecordingValidation.Validate(options);token.ThrowIfCancellationRequested();
        var result=await runner.RunAsync(path,["-hide_banner","-h","encoder="+options.EncoderName],token);
        token.ThrowIfCancellationRequested();
        var help=result.StandardOutput+"\n"+result.StandardError;
        if(result.ExitCode!=0||!help.Split('\n').Any(l=>l.TrimStart().StartsWith("Encoder "+options.EncoderName+":",StringComparison.Ordinal)||l.TrimStart().StartsWith("Encoder "+options.EncoderName+" [",StringComparison.Ordinal)))
            throw new InvalidDataException(Loc.F("Could not read FFmpeg options for encoder {0}.",options.EncoderName));
        var matches=Regex.Matches(help,@"^[ \t]+-?([a-zA-Z0-9_-]+)[ \t]+<([^>]+)>[^\r\n]*",RegexOptions.Multiline|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(200));
        var rows=Enumerable.Range(0,matches.Count).GroupBy(i=>matches[i].Groups[1].Value,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>
            {
                var index=g.First();var start=matches[index].Index;var end=index+1<matches.Count?matches[index+1].Index:help.Length;
                return help[start..end];
            },StringComparer.Ordinal);
        var arguments=new List<string>();
        FfmpegCommandBuilder.AddEncoding(arguments,options);
        var privateOptions=new HashSet<string>{"-preset","-tune","-quality","-crf","-rc","-cq","-qp_i","-qp_p","-cpu-used"};
        for(var i=0;i+1<arguments.Count;i++)
        {
            if(!privateOptions.Contains(arguments[i]))continue;
            var name=arguments[i][1..];var value=arguments[i+1];
            if(!rows.TryGetValue(name,out var row))throw new ArgumentException(Loc.F("Encoder {0} does not list option {1}.",options.EncoderName,name));
            var constants=Regex.Matches(row,@"^[ \t]+([A-Za-z][A-Za-z0-9_]*)[ \t]+(-?[0-9]+)[ \t]+[.A-Z]+(?:[ \t\r]|$)",RegexOptions.Multiline|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
            var numericValue=value;
            if(constants.Count>0)
            {
                var constant=constants.FirstOrDefault(c=>c.Groups[1].Value==value||c.Groups[2].Value==value);
                if(constant is null)throw new ArgumentException(Loc.F("Encoder {0} does not support {1}={2} in this FFmpeg build.",options.EncoderName,name,value));
                numericValue=constant.Groups[2].Value;
            }
            var range=Regex.Match(row,@"\(from (-?[0-9]+(?:\.[0-9]+)?) to (-?[0-9]+(?:\.[0-9]+)?)\)",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
            if(range.Success&&double.TryParse(numericValue,NumberStyles.Float,CultureInfo.InvariantCulture,out var numeric)&&
                (numeric<double.Parse(range.Groups[1].Value,CultureInfo.InvariantCulture)||numeric>double.Parse(range.Groups[2].Value,CultureInfo.InvariantCulture)))
                throw new ArgumentException(Loc.F("Encoder {0} does not support {1}={2} in this FFmpeg build.",options.EncoderName,name,value));
        }
    }
}
