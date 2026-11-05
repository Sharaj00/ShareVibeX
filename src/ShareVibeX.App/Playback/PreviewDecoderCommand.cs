using System.Globalization;
using ShareVibeX.Core.Playback;

namespace ShareVibeX.App.Playback;

public static class PreviewDecoderCommand
{
    private static List<string> Input(PreviewMedia media,TimeSpan position)
    {
        if(string.IsNullOrWhiteSpace(media.Path)||media.Path.Contains('\0')||position<TimeSpan.Zero)throw new ArgumentException(Loc.T("A recording file and nonnegative position are required."));
        List<string> args=["-hide_banner","-nostdin","-loglevel","warning","-ss",position.TotalSeconds.ToString("0.#########",CultureInfo.InvariantCulture),"-i",media.Path];
        if(Path.GetExtension(media.Path).ToLowerInvariant() is ".gif" or ".webp" or ".apng")args.InsertRange(args.IndexOf("-i"),["-ignore_loop","1"]);
        return args;
    }
    public static List<string> Video(PreviewMedia media,PreviewFormat format,TimeSpan position)
    {
        if(format.Width<=0||format.Width>960||format.Height<=0||format.Height>540||format.FrameRate<=0||format.FrameRate>30)throw new ArgumentOutOfRangeException(nameof(format));
        var args=Input(media,position);
        args.AddRange(["-map","0:v:0","-an","-vf",$"format=bgra,scale={format.Width}:{format.Height}:force_original_aspect_ratio=decrease,pad={format.Width}:{format.Height}:(ow-iw)/2:(oh-ih)/2,fps={format.FrameRate}","-pix_fmt","bgra","-f","rawvideo","pipe:1"]);return args;
    }
    public static List<string> Audio(PreviewMedia media,TimeSpan position)
    {
        var args=Input(media,position);args.AddRange(["-map","0:a:0","-vn","-ac","2","-ar","48000","-c:a","pcm_f32le","-f","f32le","pipe:1"]);return args;
    }
}
