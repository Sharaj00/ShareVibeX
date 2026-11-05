using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;
using R=ShareVibeX.Core.Capture.PixelRect;
using P=ShareVibeX.Core.Capture.PixelPoint;
using Monitor=ShareVibeX.Core.Capture.MonitorInfo;

namespace ShareVibeX.App.Editing;

public sealed class EditorSurface(CaptureEditorState state,DesktopLayout layout,Monitor monitor) : Control
{
    public EditorSurface(CaptureEditorState state,DesktopSnapshot snapshot,Monitor monitor)
        : this(state,new DesktopLayout(snapshot.Bounds,snapshot.Monitors),monitor) { }
    public ShareVibeX.Core.Settings.SelectionOptions Options {get;set;}=new();
    public R? ActiveMonitorBounds {get;set;}
    public Func<P,bool,R?>? DetectRegion {get;set;}
    private long lastDetection;private R? clickCandidate;
    private R RegionBounds=>Options.RestrictToActiveMonitor&&ActiveMonitorBounds is {} active&&!active.IsEmpty?active:layout.Bounds;
    public Bitmap? Image { get; set; }
    public bool IsLiveSelection { get; set; }
    public Action<bool>? Changed { get; set; }
    public Action? Cancel { get; set; }
    public Action<Annotation>? TextEditRequested { get; set; }
    public Action? BeforePointerDown { get; set; }
    public bool Busy { get; set; }
    private P? origin;
    private R originalRegion;
    private Annotation? originalAnnotation;
    private readonly List<P> stroke=[];
    private int edge;
    private bool moveRegion;
    private EditorTool gesture;

    protected override void OnInitialized(){base.OnInitialized();Focusable=true;Cursor=new Cursor(StandardCursorType.Cross);}
    private P Physical(PointerEventArgs e){var point=this.PointToScreen(e.GetPosition(this));return new(point.X,point.Y);}

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if(!IsLiveSelection && Image is null)return;
        var scale=TopLevel.GetTopLevel(this)?.RenderScaling??monitor.Scale;
        var source=monitor.Bounds.ToFrameRect(layout.Bounds);
        if(!IsLiveSelection)context.DrawImage(Image!,new Rect(source.X,source.Y,source.Width,source.Height),new Rect(Bounds.Size));
        else context.FillRectangle(Brushes.Transparent,new Rect(Bounds.Size));
        var selection=state.Region.Intersect(monitor.Bounds);
        var dim=new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(Options.EditorDimPercent,0,90)*255d/100),0,0,0));
        if(selection.IsEmpty) { if(!IsLiveSelection)context.FillRectangle(dim,new Rect(Bounds.Size)); }
        else
        {
            var rect=Local(selection,scale);
            if(!IsLiveSelection)
            {
            context.FillRectangle(dim,new Rect(0,0,Bounds.Width,rect.Top));
            context.FillRectangle(dim,new Rect(0,rect.Bottom,Bounds.Width,Math.Max(0,Bounds.Height-rect.Bottom)));
            context.FillRectangle(dim,new Rect(0,rect.Top,rect.Left,rect.Height));
            context.FillRectangle(dim,new Rect(rect.Right,rect.Top,Math.Max(0,Bounds.Width-rect.Right),rect.Height));
            }
            context.DrawRectangle(null,ShareVibeX.App.Views.CaptureOutline.CreatePen(Options,scale),rect);
            if(state.Tool==EditorTool.Region && (!IsLiveSelection || !state.IsDrawing))
            foreach(var point in new[]{rect.TopLeft,rect.TopRight,rect.BottomLeft,rect.BottomRight,new Point(rect.Center.X,rect.Top),new Point(rect.Center.X,rect.Bottom),new Point(rect.Left,rect.Center.Y),new Point(rect.Right,rect.Center.Y)})
                context.FillRectangle(Brushes.White,new Rect(point.X-3,point.Y-3,6,6));
        }
        if(IsLiveSelection&&state.Region.IsEmpty&&state.HoverRegion is {} hovered)
            context.DrawRectangle(null,new Pen(Brush.Parse("#78D3FF"),1/scale),Local(hovered.Intersect(monitor.Bounds),scale));
        if(Options.ScreenCrosshair&&state.CursorPosition is {} cross)
        {
            var cx=(cross.X-monitor.Bounds.X)/scale;var cy=(cross.Y-monitor.Bounds.Y)/scale;var pen=new Pen(Brush.Parse("#9978D3FF"),1/scale);
            if(cx>=0&&cx<=Bounds.Width)context.DrawLine(pen,new(cx,0),new(cx,Bounds.Height));
            if(cy>=0&&cy<=Bounds.Height)context.DrawLine(pen,new(0,cy),new(Bounds.Width,cy));
        }
        if(Options.CenterCrosshair&&!state.Region.IsEmpty)
        {
            var center=new Point((state.Region.X+state.Region.Width/2d-monitor.Bounds.X)/scale,(state.Region.Y+state.Region.Height/2d-monitor.Bounds.Y)/scale);var pen=new Pen(Brush.Parse("#78D3FF"),1/scale);
            context.DrawLine(pen,new(center.X-8,center.Y),new(center.X+8,center.Y));context.DrawLine(pen,new(center.X,center.Y-8),new(center.X,center.Y+8));
        }
        if((state.MoveModifier || gesture==EditorTool.Select && origin is not null) && state.Selected is {} selected)
            context.DrawRectangle(null,new Pen(Brush.Parse("#FFE677"),1),Local(selected.Bounds,scale));
        if(IsLiveSelection && state.IsDrawing && (Options.ShowSize||Options.ShowPosition) && !state.Region.IsEmpty && state.CursorPosition is {} cursor &&
            cursor.X>=monitor.Bounds.X && cursor.X<monitor.Bounds.Right && cursor.Y>=monitor.Bounds.Y && cursor.Y<monitor.Bounds.Bottom)
        {
            var text=new FormattedText(SelectionRules.FormatInfo(Options,state.Region),System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,new Typeface("Segoe UI"),13,Brushes.White);
            var x=Math.Clamp((cursor.X-monitor.Bounds.X)/scale+14,8,Math.Max(8,Bounds.Width-text.Width-20));
            var y=Math.Clamp((cursor.Y-monitor.Bounds.Y)/scale+18,8,Math.Max(8,Bounds.Height-text.Height-16));
            context.FillRectangle(Brush.Parse("#EE20242B"),new Rect(x-6,y-4,text.Width+12,text.Height+8),4);
            context.DrawText(text,new Point(x,y));
        }
        if(!IsLiveSelection && state.Region.IsEmpty)
        {
            var text=new FormattedText(Loc.T("Drag to select an area · Esc to cancel"),System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),15,Brushes.White);
            var x=Math.Max(12,(Bounds.Width-text.Width)/2);
            context.FillRectangle(Brush.Parse("#E820242B"),new Rect(x-12,18,text.Width+24,40),8);
            context.DrawText(text,new Point(x,28));
        }
    }
    private Rect Local(R rect,double scale)=>new((rect.X-monitor.Bounds.X)/scale,(rect.Y-monitor.Bounds.Y)/scale,rect.Width/scale,rect.Height/scale);
    private static bool Contains(R rect,P point,int padding=0)=>point.X>=rect.X-padding && point.Y>=rect.Y-padding && point.X<=rect.Right+padding && point.Y<=rect.Bottom+padding;

    public Func<P,bool>? PickColor {get;init;}

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if(Busy)return;
        if(e.GetCurrentPoint(this).Properties.IsRightButtonPressed){Cancel?.Invoke();return;}
        if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;
        if(PickColor?.Invoke(Physical(e))==true){e.Handled=true;return;}
        BeforePointerDown?.Invoke();
        Focus();var p=Physical(e);if(IsLiveSelection||state.Tool==EditorTool.Region)p=SelectionRules.ClampPoint(p,RegionBounds);
        if(IsLiveSelection&&state.Region.IsEmpty&&Options.DetectWindows&&DetectRegion is not null)
        {
            lastDetection=System.Diagnostics.Stopwatch.GetTimestamp();
            try{state.HoverRegion=DetectRegion(p,Options.DetectControls)?.Intersect(RegionBounds);}catch{state.HoverRegion=null;}
        }
        clickCandidate=IsLiveSelection&&state.Region.IsEmpty&&state.HoverRegion is {} hovered&&Contains(hovered,p)?hovered:null;state.HoverRegion=null;state.CursorPosition=p;origin=p;stroke.Clear();stroke.Add(p);originalAnnotation=null;edge=0;moveRegion=false;originalRegion=state.Region;
        if(IsLiveSelection || state.Region.IsEmpty)state.Tool=EditorTool.Region;
        gesture=!IsLiveSelection && !state.Region.IsEmpty && e.KeyModifiers.HasFlag(KeyModifiers.Control)?EditorTool.Select:state.Tool;
        state.IsDrawing=true;
        if(gesture!=EditorTool.Select)state.SelectedId=null;
        if(gesture==EditorTool.Region)
        {
            var margin=(int)(8*(TopLevel.GetTopLevel(this)?.RenderScaling??1));
            if(!state.Region.IsEmpty && Contains(state.Region,p,margin))
            {
                if(Math.Abs(p.X-state.Region.X)<=margin)edge|=1;
                if(Math.Abs(p.X-state.Region.Right)<=margin)edge|=2;
                if(Math.Abs(p.Y-state.Region.Y)<=margin)edge|=4;
                if(Math.Abs(p.Y-state.Region.Bottom)<=margin)edge|=8;
                moveRegion=edge==0;
            }
            else state.Region=default;
        }
        else if(gesture==EditorTool.Select)
        {
            originalAnnotation=state.Document.Items.LastOrDefault(item=>Contains(item.Bounds,p,5));
            state.SelectedId=originalAnnotation?.Id;
            if(originalAnnotation is not null)state.LoadStyle(originalAnnotation);
        }
        else if(gesture==EditorTool.Text)
        {
            originalAnnotation=state.Document.Items.LastOrDefault(item=>item.Kind==AnnotationKind.Text && Contains(item.Bounds,p));
        }
        e.Pointer.Capture(this);Changed?.Invoke(false);e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if(Busy)return;
        var p=Physical(e);state.CursorPosition=p;
        if(origin is not {} from)
        {
            if(IsLiveSelection&&state.Region.IsEmpty&&Options.DetectWindows&&DetectRegion is not null&&System.Diagnostics.Stopwatch.GetElapsedTime(lastDetection).TotalMilliseconds>=33)
            {
                lastDetection=System.Diagnostics.Stopwatch.GetTimestamp();
                try{state.HoverRegion=DetectRegion(p,Options.DetectControls)?.Intersect(RegionBounds);}catch{state.HoverRegion=null;}
            }
            if(Options.ScreenCrosshair||Options.CenterCrosshair||Options.DetectWindows)Changed?.Invoke(false);
            return;
        }
        if(gesture==EditorTool.Region)p=SelectionRules.ClampPoint(p,RegionBounds);
        var dx=p.X-from.X;var dy=p.Y-from.Y;
        if(gesture==EditorTool.Region)
        {
            if(moveRegion)
                state.Region=SelectionRules.Move(originalRegion,dx,dy,RegionBounds);
            else if(edge!=0)
            {
                var x=(edge&1)!=0?p.X:originalRegion.X;var right=(edge&2)!=0?p.X:originalRegion.Right;
                var y=(edge&4)!=0?p.Y:originalRegion.Y;var bottom=(edge&8)!=0?p.Y:originalRegion.Bottom;
                state.Region=R.FromPoints(new(x,y),new(right,bottom)).Intersect(RegionBounds);
            }
            else
            {
                state.Region=SelectionRules.Drag(from,p,RegionBounds,e.KeyModifiers.HasFlag(KeyModifiers.Shift),e.KeyModifiers.HasFlag(KeyModifiers.Alt),Options.SnapSizes);
            }
        }
        else if(gesture==EditorTool.Select)state.Draft=originalAnnotation?.Move(dx,dy);
        else if(gesture==EditorTool.Text && originalAnnotation is not null) { }
        else
        {
            if(gesture==EditorTool.Pen)stroke.Add(p);else{stroke.Clear();stroke.Add(from);stroke.Add(p);}
            state.Draft=state.NewAnnotation(stroke,gesture);
        }
        Changed?.Invoke(false);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if(origin is null)return;
        OnPointerMoved(e);var from=origin.Value;
        if(IsLiveSelection&&clickCandidate is {} candidate)
        {var point=Physical(e);if(Math.Abs(point.X-from.X)<=3&&Math.Abs(point.Y-from.Y)<=3)state.Region=candidate.Intersect(RegionBounds);}
        clickCandidate=null;origin=null;state.IsDrawing=false;e.Pointer.Capture(null);
        if(gesture==EditorTool.Text)
        {
            var text=originalAnnotation??state.Draft??state.NewAnnotation([from,from],EditorTool.Text);state.Draft=null;
            TextEditRequested?.Invoke(text);Changed?.Invoke(true);e.Handled=true;return;
        }
        if(state.Draft is {} draft)
        {
            if(gesture==EditorTool.Select)
            {
                if(originalAnnotation is {} original && (draft.Bounds!=original.Bounds || !draft.Points.SequenceEqual(original.Points)))state.Document.Replace(draft);
            }
            else if(!draft.Bounds.IsEmpty || draft.Points.Count>=2){state.Document.Add(draft);state.SelectedId=draft.Id;}
            state.Draft=null;
        }
        Changed?.Invoke(true);e.Handled=true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        if(origin is not null){origin=null;state.IsDrawing=false;state.Draft=null;Changed?.Invoke(true);}
        base.OnPointerCaptureLost(e);
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if(Busy || origin is null || gesture is EditorTool.Region or EditorTool.Select or EditorTool.Text)return;
        if(state.AdjustThickness(e.Delta.Y))Changed?.Invoke(true);
        e.Handled=true;
    }
}


