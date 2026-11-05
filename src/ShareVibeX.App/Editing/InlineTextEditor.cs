using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;
using R=ShareVibeX.Core.Capture.PixelRect;
using P=ShareVibeX.Core.Capture.PixelPoint;

namespace ShareVibeX.App.Editing;

public sealed class InlineTextEditor:Border
{
    private readonly Annotation original;
    private readonly R monitor;
    private readonly R desktop;
    private readonly double scale;
    private readonly Action<Annotation> commit;
    private readonly Action cancel;
    private bool finished;
    private bool resizing,moving;
    private P dragStart;
    private R dragBounds;
    private uint color;
    public TextBox Input {get;}
    public R Area {get;private set;}

    public InlineTextEditor(Annotation annotation,R monitor,double scale,Action<Annotation> commit,Action cancel,R? desktopBounds=null)
    {
        original=annotation;this.monitor=monitor;desktop=desktopBounds??monitor;this.scale=scale;this.commit=commit;this.cancel=cancel;color=annotation.Color;
        BorderThickness=new Thickness(1/scale);BorderBrush=Brush.Parse("#78D3FF");Background=Brush.Parse("#18101010");
        Input=new TextBox{Text=annotation.Text,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,Padding=new Thickness((FittedTextLayout.Padding-1)/scale),BorderThickness=new Thickness(0),Background=Brushes.Transparent,MinHeight=0,
            FontFamily=new FontFamily("Segoe UI, Inter, sans-serif"),VerticalContentAlignment=VerticalAlignment.Top};
        AutomationProperties.SetName(Input,"Screenshot text");
        ScrollViewer.SetHorizontalScrollBarVisibility(Input,Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(Input,Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        var grip=new Border{Width=12,Height=12,Background=Brush.Parse("#78D3FF"),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Cursor=new Cursor(StandardCursorType.BottomRightCorner)};
        AutomationProperties.SetName(grip,"Resize text area");
        Child=new Grid{Children={Input,grip}};
        Input.TextChanged+=(_,_)=>Fit();
        Input.KeyDown+=(_,args)=>
        {
            if(args.Key==Key.Escape){Cancel();args.Handled=true;}
            else if(args.Key==Key.Enter && args.KeyModifiers.HasFlag(KeyModifiers.Control)){Commit();args.Handled=true;}
        };
        Input.LostFocus+=(_,_)=>Dispatcher.UIThread.Post(()=>{if(!finished && !resizing && !moving && !Input.IsKeyboardFocusWithin)Commit();},DispatcherPriority.Background);
        grip.PointerPressed+=(_,args)=>
        {
            if(!args.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)return;
            resizing=true;dragStart=Physical(args);dragBounds=Area;args.Pointer.Capture(this);args.Handled=true;
        };
        AddHandler(PointerPressedEvent,(_,args)=>
        {
            if(!args.KeyModifiers.HasFlag(KeyModifiers.Control) || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;
            moving=true;dragStart=Physical(args);dragBounds=Area;args.Pointer.Capture(this);args.Handled=true;
        },RoutingStrategies.Tunnel);
        ResizeTo(annotation.Bounds);SetColor(color);
    }
    private P Physical(PointerEventArgs args){var p=this.PointToScreen(args.GetPosition(this));return new(p.X,p.Y);}
    public void ResizeTo(R requested)
    {
        var width=Math.Clamp(requested.Width,Math.Min(32,desktop.Width),desktop.Width);var height=Math.Clamp(requested.Height,Math.Min(24,desktop.Height),desktop.Height);
        Area=new(Math.Clamp(requested.X,desktop.X,desktop.Right-width),Math.Clamp(requested.Y,desktop.Y,desktop.Bottom-height),width,height);
        Width=Area.Width/scale;Height=Area.Height/scale;Canvas.SetLeft(this,(Area.X-monitor.X)/scale);Canvas.SetTop(this,(Area.Y-monitor.Y)/scale);Fit();
    }
    private void Fit()
    {
        if(Input is null)return;var size=FittedTextLayout.FontSize(Input.Text??"",Area)/scale;Input.FontSize=size;Input.LineHeight=size*1.3;
    }
    public void SetColor(uint value){color=value;Input.Foreground=new SolidColorBrush(Color.FromUInt32(value));}
    public void Commit()
    {
        if(finished)return;finished=true;
        var text=Input.Text??"";commit(original with{Bounds=Area,Text=text,Color=color,FontSize=(int)Math.Round(FittedTextLayout.FontSize(text,Area))});
    }
    public void Cancel(){if(finished)return;finished=true;cancel();}
    protected override void OnPointerMoved(PointerEventArgs args)
    {
        if(!resizing && !moving)return;var p=Physical(args);var x=p.X-dragStart.X;var y=p.Y-dragStart.Y;
        ResizeTo(resizing?dragBounds with{Width=dragBounds.Width+x,Height=dragBounds.Height+y}:dragBounds with{X=dragBounds.X+x,Y=dragBounds.Y+y});args.Handled=true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs args)
    {
        if(!resizing && !moving)return;OnPointerMoved(args);resizing=moving=false;args.Pointer.Capture(null);Input.Focus();args.Handled=true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs args){resizing=moving=false;base.OnPointerCaptureLost(args);}
}
