using System.Globalization;
using System.Text.RegularExpressions;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Capture;

public static class SelectionRules
{
    public static IReadOnlyList<(int Width,int Height)> ParseSnapSizes(string text)
    {
        var result=new List<(int,int)>();
        foreach(var part in text.Split(new[]{';','\r','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var match=Regex.Match(part,@"\A([1-9]\d{0,4})[xX×]([1-9]\d{0,4})\z");
            if(!match.Success)throw new ArgumentException(Loc.T("Use snap sizes such as 640x480;1280x720."));
            var width=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);var height=int.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture);
            if(width>32768||height>32768)throw new ArgumentException(Loc.T("Snap dimensions must not exceed 32768 pixels."));
            if(!result.Contains((width,height)))result.Add((width,height));
            if(result.Count>20)throw new ArgumentException(Loc.T("Use at most 20 snap sizes."));
        }
        return result;
    }
    public static void Validate(SelectionOptions options)
    {
        if(!Enum.IsDefined(options.OutlineStyle)||options.OutlineOpacity is <0 or >100||options.OutlineThickness is <1 or >12||options.OutlineColor is not {Length:7} color||color[0]!='#'||!color[1..].All(char.IsAsciiHexDigit))
            throw new ArgumentException(Loc.T("Enter a frame color in #RRGGBB format and an opacity between 0 and 100."));
        if(options.EditorDimPercent is <0 or >90)throw new ArgumentException(Loc.T("Editor dim strength must be between 0 and 90 percent."));
        if(options.InfoTemplate is null||options.InfoTemplate.Length>256||options.SnapSizes is null||options.SnapSizes.Length>512)throw new ArgumentException(Loc.T("Selection information or snap sizes are too long."));
        var without=Regex.Replace(options.InfoTemplate,@"\{(x|y|width|height|newline)\}","");
        if(without.Contains('{')||without.Contains('}'))throw new ArgumentException(Loc.T("Unknown selection information token. Use {x}, {y}, {width}, {height}, {newline}."));
        ParseSnapSizes(options.SnapSizes);
    }
    public static string FormatInfo(SelectionOptions options,PixelRect region)
    {
        if(!options.ShowSize)return options.ShowPosition?$"X: {region.X}  Y: {region.Y}":"";
        var text=options.InfoTemplate.Replace("{x}",region.X.ToString(CultureInfo.InvariantCulture)).Replace("{y}",region.Y.ToString(CultureInfo.InvariantCulture))
            .Replace("{width}",region.Width.ToString(CultureInfo.InvariantCulture)).Replace("{height}",region.Height.ToString(CultureInfo.InvariantCulture)).Replace("{newline}","\n");
        if(options.ShowPosition&&!options.InfoTemplate.Contains("{x}")&&!options.InfoTemplate.Contains("{y}"))text=$"X: {region.X}  Y: {region.Y}\n"+text;
        return text;
    }
    public static PixelPoint ClampPoint(PixelPoint point,PixelRect bounds)=>new(Math.Clamp(point.X,bounds.X,bounds.Right),Math.Clamp(point.Y,bounds.Y,bounds.Bottom));
    public static PixelRect Move(PixelRect rectangle,int dx,int dy,PixelRect bounds)
    {
        var width=Math.Min(rectangle.Width,bounds.Width);var height=Math.Min(rectangle.Height,bounds.Height);
        return new(Math.Clamp(rectangle.X+dx,bounds.X,bounds.Right-width),Math.Clamp(rectangle.Y+dy,bounds.Y,bounds.Bottom-height),width,height);
    }
    public static PixelRect Drag(PixelPoint from,PixelPoint to,PixelRect bounds,bool square,bool snap,string sizes)
    {
        from=ClampPoint(from,bounds);to=ClampPoint(to,bounds);var dx=to.X-from.X;var dy=to.Y-from.Y;
        if(square){var size=Math.Min(Math.Abs(dx),Math.Abs(dy));to=new(from.X+Math.Sign(dx)*size,from.Y+Math.Sign(dy)*size);}
        else if(snap)
        {
            var availableWidth=dx<0?from.X-bounds.X:bounds.Right-from.X;var availableHeight=dy<0?from.Y-bounds.Y:bounds.Bottom-from.Y;
            var candidates=ParseSnapSizes(sizes).Where(s=>s.Width<=availableWidth&&s.Height<=availableHeight).ToArray();
            if(candidates.Length>0)
            {
                var nearest=candidates.MinBy(s=>(long)(s.Width-Math.Abs(dx))*(s.Width-Math.Abs(dx))+(long)(s.Height-Math.Abs(dy))*(s.Height-Math.Abs(dy)));
                to=new(from.X+(dx<0?-1:1)*nearest.Width,from.Y+(dy<0?-1:1)*nearest.Height);
            }
        }
        return PixelRect.FromPoints(from,to).Intersect(bounds);
    }
}
