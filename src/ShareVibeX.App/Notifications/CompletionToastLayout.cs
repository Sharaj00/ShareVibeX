using Avalonia;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Notifications;

public static class CompletionToastLayout
{
    public static (PixelPoint Position,Size Size) Place(PixelRect workArea,double scale,CompletionOptions options)
    {
        if(!double.IsFinite(scale)||scale<=0)throw new ArgumentOutOfRangeException(nameof(scale));
        var margin=Math.Min(12,Math.Min(workArea.Width,workArea.Height)/(4*scale));
        var width=Math.Max(1,Math.Min(options.Width,workArea.Width/scale-2*margin));
        var height=Math.Max(1,Math.Min(options.Height,workArea.Height/scale-2*margin));
        var left=options.Placement is CompletionPlacement.BottomLeft or CompletionPlacement.TopLeft;
        var top=options.Placement is CompletionPlacement.TopLeft or CompletionPlacement.TopRight;
        var x=left?workArea.X+margin*scale:workArea.Right-(width+margin)*scale;
        var y=top?workArea.Y+margin*scale:workArea.Bottom-(height+margin)*scale;
        return(new((int)Math.Round(x),(int)Math.Round(y)),new(width,height));
    }
    public static double Opacity(TimeSpan elapsed,CompletionOptions options)
    {
        var duration=Math.Max(0,options.DurationSeconds);var seconds=elapsed.TotalSeconds;
        if(seconds>=duration)return 0;
        var fade=Math.Clamp(options.FadeSeconds,0,duration);
        return fade==0?1:Math.Clamp((duration-seconds)/fade,0,1);
    }
}
