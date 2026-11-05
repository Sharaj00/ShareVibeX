using Avalonia.Media;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Views;

internal static class CaptureOutline
{
    public static Pen CreatePen(SelectionOptions options,double scale)
    {
        var color=Color.Parse(options.OutlineColor);
        return new Pen
        {
            Brush=new SolidColorBrush(color,options.OutlineOpacity/100d),
            Thickness=1/scale,
            LineCap=options.OutlineStyle==CaptureOutlineStyle.Dotted?PenLineCap.Round:PenLineCap.Flat,
            DashStyle=options.OutlineStyle switch
            {
                CaptureOutlineStyle.Dashed=>DashStyle.Dash,
                CaptureOutlineStyle.Dotted=>new DashStyle([1,4],0),
                _=>null
            }
        };
    }
}
