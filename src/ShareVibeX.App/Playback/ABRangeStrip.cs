using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ShareVibeX.App.Playback;

public sealed class ABRangeStrip:Control
{
    private enum Gesture {None,A,B,Both,Seek}
    private Gesture gesture;
    private long pressedFrame;
    public long FrameCount {get;private set;}=1;
    public long A {get;private set;}
    public long B {get;private set;}=1;
    public long PositionFrame {get;private set;}
    public bool IsDragging=>gesture!=Gesture.None;
    public event Action? Changed;
    public event Action<long>? SeekRequested;
    public ABRangeStrip(){Height=34;MinWidth=40;Focusable=true;AutomationProperties.SetName(this,"Playback position and A–B selection");ToolTip.SetTip(this,Loc.T("Drag upper A/B handles; drag the lower track to seek. Arrows: A; Shift+arrows: B"));ToolTip.SetShouldUseOverlayLayer(this,true);}
    public void Reset(long frames){FrameCount=Math.Max(1,frames);PositionFrame=0;SetRange(0,FrameCount);}
    public void SetRange(long a,long b){A=Math.Clamp(a,0,FrameCount);B=Math.Clamp(b,A,FrameCount);InvalidateVisual();Changed?.Invoke();}
    public void SetPosition(long frame){if(IsDragging&&gesture==Gesture.Seek)return;PositionFrame=Math.Clamp(frame,0,FrameCount);InvalidateVisual();}
    public void SeekTo(long frame){PositionFrame=Math.Clamp(frame,0,FrameCount-1);InvalidateVisual();SeekRequested?.Invoke(PositionFrame);}
    private double X(long frame)=>6+Math.Max(1,Bounds.Width-12)*frame/FrameCount;
    public override void Render(DrawingContext context)
    {
        base.Render(context);context.DrawRectangle(Brushes.Transparent,null,new Rect(Bounds.Size));var width=Math.Max(1,Bounds.Width-12);var a=X(A);var b=X(B);var play=X(PositionFrame);
        context.DrawRectangle(Brushes.DimGray,null,new Rect(6,17,width,8),4,4);context.DrawRectangle(Brushes.DeepSkyBlue,null,new Rect(a,17,Math.Max(1,b-a),8),4,4);
        context.DrawLine(new Pen(Brushes.OrangeRed,2),new(play,15),new(play,32));
    }
    private long Frame(Point point)=>(long)Math.Round(Math.Clamp((point.X-6)/Math.Max(1,Bounds.Width-12),0,1)*FrameCount);
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;Focus();var point=e.GetPosition(this);
        var distanceA=Math.Abs(point.X-X(A));var distanceB=Math.Abs(point.X-X(B));pressedFrame=Frame(point);
        gesture=Math.Min(distanceA,distanceB)<=8
            ?Math.Abs(distanceA-distanceB)<.01?Gesture.Both:distanceA<distanceB?Gesture.A:Gesture.B
            :Gesture.Seek;
        e.Pointer.Capture(this);Move(Frame(point));e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e){base.OnPointerMoved(e);if(e.Pointer.Captured==this)Move(Frame(e.GetPosition(this)));}
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {base.OnPointerReleased(e);if(e.Pointer.Captured!=this)return;Move(Frame(e.GetPosition(this)));var seek=gesture==Gesture.Seek;gesture=Gesture.None;e.Pointer.Capture(null);if(seek)SeekTo(PositionFrame);Changed?.Invoke();e.Handled=true;}
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e){base.OnPointerCaptureLost(e);gesture=Gesture.None;Changed?.Invoke();}
    private void Move(long frame)
    {
        if(gesture==Gesture.Both){if(frame==pressedFrame)return;gesture=frame>pressedFrame?Gesture.B:Gesture.A;}
        if(gesture==Gesture.A)SetRange(Math.Min(frame,B),B);else if(gesture==Gesture.B)SetRange(A,Math.Max(frame,A));else if(gesture==Gesture.Seek){PositionFrame=Math.Clamp(frame,0,FrameCount-1);InvalidateVisual();}
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {base.OnKeyDown(e);if(e.Key is not(Key.Left or Key.Right))return;var step=e.Key==Key.Left?-1:1;if(e.KeyModifiers.HasFlag(KeyModifiers.Shift))SetRange(A,Math.Clamp(B+step,A,FrameCount));else SetRange(Math.Clamp(A+step,0,B),B);e.Handled=true;}
}


