using System.Globalization;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Core.Recording;

public sealed record WgcVideoMonitor(MonitorInfo Monitor,ulong Handle);

public sealed class WgcVideoSource
{
    public PixelRect Region {get;}
    public string InputGraph {get;}
    public int MonitorCount {get;}
    public int FrameRate {get;}
    public bool IncludeCursor {get;}
    private WgcVideoSource(PixelRect region,string graph,int count,int frameRate,bool cursor)
    {Region=region;InputGraph=graph;MonitorCount=count;FrameRate=frameRate;IncludeCursor=cursor;}

    public static WgcVideoSource Create(PixelRect region,IReadOnlyList<WgcVideoMonitor> monitors,int frameRate,bool cursor)
    {
        if(region.IsEmpty||region.Width>32768||region.Height>32768||frameRate is <1 or >240)throw new ArgumentOutOfRangeException(nameof(region));
        ArgumentNullException.ThrowIfNull(monitors);
        var selected=monitors.Where(m=>!m.Monitor.Bounds.Intersect(region).IsEmpty).OrderBy(m=>m.Monitor.Id,StringComparer.Ordinal).ToArray();
        if(selected.Length==0)throw new CaptureSourceUnavailableException(Loc.T("No displays intersect the recording region."));
        if(selected.Any(m=>m.Handle==0||m.Monitor.Bounds.IsEmpty||!double.IsFinite(m.Monitor.Scale)||m.Monitor.Scale<=0)||
            selected.Select(m=>m.Monitor.Id).Distinct(StringComparer.Ordinal).Count()!=selected.Length||selected.Select(m=>m.Handle).Distinct().Count()!=selected.Length)
            throw new InvalidDataException(Loc.T("The display layout changed. Start a new capture."));
        static string N(long value)=>value.ToString(CultureInfo.InvariantCulture);
        var parts=new List<string>();
        for(var i=0;i<selected.Length;i++)
        {
            var target=selected[i];var b=target.Monitor.Bounds;var crop=b.Intersect(region);
            var source="gfxcapture=hmonitor="+target.Handle.ToString(CultureInfo.InvariantCulture)+":capture_cursor="+(cursor?"1":"0")+
                ":max_framerate="+N(frameRate)+":crop_left="+N(checked(crop.X-b.X))+":crop_top="+N(checked(crop.Y-b.Y))+
                ":crop_right="+N(checked(b.Right-crop.Right))+":crop_bottom="+N(checked(b.Bottom-crop.Bottom))+
                ":width="+N(crop.Width)+":height="+N(crop.Height)+":resize_mode=crop:output_fmt=bgra,hwdownload,format=bgra,setpts=PTS-STARTPTS";
            if(selected.Length==1&&crop==region)return new(region,source+",fps="+N(frameRate),1,frameRate,cursor);
            parts.Add(source+"[m"+N(i)+"]");
        }
        parts.Add("color=c=black:s="+N(region.Width)+"x"+N(region.Height)+":r="+N(frameRate)+",format=bgra[base0]");
        for(var i=0;i<selected.Length;i++)
        {
            var crop=selected[i].Monitor.Bounds.Intersect(region);
            parts.Add("[base"+N(i)+"][m"+N(i)+"]overlay=x="+N(checked(crop.X-region.X))+":y="+N(checked(crop.Y-region.Y))+
                ":shortest=1:format=rgb:alpha=straight[base"+N(i+1)+"]");
        }
        parts.Add("[base"+N(selected.Length)+"]fps="+N(frameRate));
        return new(region,string.Join(';',parts),selected.Length,frameRate,cursor);
    }
}
