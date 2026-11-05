using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows;
using R=ShareVibeX.Core.Capture.PixelRect;
namespace ShareVibeX.App.Recording;
public sealed class RecordingRegionFrame:IDisposable
{
    private readonly List<Window> windows=[];

    public IReadOnlyList<Window> Windows=>windows.AsReadOnly();
    public void Show(R region,IReadOnlyList<MonitorInfo> monitors,Func<nint,bool>? exclude=null,Func<nint,int,int,bool>? mask=null,ShareVibeX.Core.Settings.SelectionOptions? options=null)
    {
        Dispose();windows.Clear();
        var monitor=monitors.OrderByDescending(m=>(long)m.Bounds.Intersect(region).Width*m.Bounds.Intersect(region).Height).First();
        const int margin=1;
        var width=checked(region.Width+2*margin);var height=checked(region.Height+2*margin);
        Avalonia.PixelPoint PositionFor(R area)=>new(checked(area.X-margin),checked(area.Y-margin));
        var position=PositionFor(region);
        var window=new Window{Title=Loc.T("ShareVibeX · Recording border"),WindowDecorations=WindowDecorations.None,CanResize=false,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Width=width/monitor.Scale,Height=height/monitor.Scale,Position=position};
        CapturePanelStyle.Apply(window);windows.Add(window);
        try
        {
            var handle=window.TryGetPlatformHandle()?.Handle??0;
            bool ApplyMask(nint hwnd)=>mask?.Invoke(hwnd,width,height)??WindowsFrameRegion.Apply(hwnd,width,height,margin+1);
            if(!(exclude??WindowsCaptureExclusion.TryExclude)(handle)||!ApplyMask(handle)){window.Close();return;}
            var outline=new Outline(margin,options??new());
            window.Content=outline;

            window.Opened+=(_,_)=>{window.Width=width/window.RenderScaling;window.Height=height/window.RenderScaling;window.Position=position;if(!ApplyMask(window.TryGetPlatformHandle()?.Handle??0))window.Close();};
            window.Show();window.Position=position;
        }
        catch{Dispose();throw;}

    }
    public void MoveTo(R region)
    {
        foreach(var window in windows)if(window.IsVisible)window.Position=new(region.X-1,region.Y-1);
    }
    public void Dispose(){foreach(var window in windows)window.Close();}
    private sealed class Outline(int margin,ShareVibeX.Core.Settings.SelectionOptions options):Control
    {
        public override void Render(DrawingContext context)
        {
            var scale=TopLevel.GetTopLevel(this)?.RenderScaling??1;var inset=margin/scale;
            var pen=CaptureOutline.CreatePen(options,scale);
            context.DrawRectangle(null,pen,new Rect(inset,inset,Math.Max(0,Bounds.Width-2*inset),Math.Max(0,Bounds.Height-2*inset)));
        }
    }
}

