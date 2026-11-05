using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using R=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Capture;

internal sealed class RegionMoveGrip:Border,IDisposable
{
    private Func<R>? getRegion;
    private Action<R,int,int>? move;
    private (Avalonia.PixelPoint Point,R Region)? drag;
    private readonly Cursor hand=new(StandardCursorType.Hand);
    private bool available;

    public RegionMoveGrip()
    {
        Height=10;Background=Brush.Parse("#805F6670");CornerRadius=new CornerRadius(4);
        AutomationProperties.SetName(this,"Move recording region");
        ToolTip.SetShouldUseOverlayLayer(this,true);
        SetAvailable(false);
    }

    public void Configure(Func<R> region,Action<R,int,int> moveRegion)
    {getRegion=region;move=moveRegion;}

    public void SetAvailable(bool value)
    {
        available=value;
        Cursor=value?hand:null;
        Opacity=value?1:0.5;
        ToolTip.SetTip(this,Loc.T(value?"Move recording region":"Moving the recording region is currently unavailable."));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if(!available||getRegion is null||e.GetCurrentPoint(this).Properties.PointerUpdateKind!=PointerUpdateKind.LeftButtonPressed)return;
        drag=(this.PointToScreen(e.GetPosition(this)),getRegion());
        e.Pointer.Capture(this);
        e.Handled=true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if(!available||drag is not {} start)return;
        var point=this.PointToScreen(e.GetPosition(this));
        move?.Invoke(start.Region,point.X-start.Point.X,point.Y-start.Point.Y);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if(drag is null||e.InitialPressMouseButton!=MouseButton.Left)return;
        drag=null;
        e.Pointer.Capture(null);
        e.Handled=true;
    }

    public void Dispose()=>hand.Dispose();

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)=>drag=null;
}
