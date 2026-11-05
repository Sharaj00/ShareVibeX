using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;

namespace ShareVibeX.App.Views;

public sealed class ClientTitleBar : Grid
{
    public ClientTitleBar(Window window)
    {
        Height=46;ColumnDefinitions=new("*,Auto");
        var dragArea=new Border {Background=Brushes.Transparent,Child=new TextBlock{Text=Loc.T("ShareVibeX  /  Settings"),FontSize=13,Opacity=0.8,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(22,0,0,0)}};
        dragArea.PointerPressed+=(_,args)=>
        {
            if(args.Handled || !args.GetCurrentPoint(dragArea).Properties.IsLeftButtonPressed)return;
            if(args.ClickCount==2)ToggleMaximize();else window.BeginMoveDrag(args);
            args.Handled=true;
        };
        var minimize=Caption("—","Minimize settings",()=>window.WindowState=WindowState.Minimized);
        var maximize=Caption("□","Maximize or restore settings",ToggleMaximize);
        var close=Caption("×","Close settings",window.Close);
        close.PointerEntered+=(_,_)=>{close.Background=Brush.Parse("#C42B1C");close.Foreground=Brushes.White;};
        close.PointerExited+=(_,_)=>{close.Background=Brushes.Transparent;close.ClearValue(Button.ForegroundProperty);};
        window.PropertyChanged+=(_,args)=>{if(args.Property==Window.WindowStateProperty)maximize.Content=window.WindowState==WindowState.Maximized?"❐":"□";};
        var controls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top,Children={minimize,maximize,close}};
        Grid.SetColumn(controls,1);Children.Add(dragArea);Children.Add(controls);
        void ToggleMaximize()=>window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    }

    private static Button Caption(string glyph,string name,Action action)
    {
        var button=Ui.Button(glyph,action);button.Width=46;button.Height=38;button.FontSize=17;button.Padding=new Thickness(0);
        button.CornerRadius=new CornerRadius(0);button.Background=Brushes.Transparent;button.BorderThickness=new Thickness(0);
        button.HorizontalContentAlignment=HorizontalAlignment.Center;AutomationProperties.SetName(button,name);ToolTip.SetTip(button,Loc.T(name));return button;
    }

    public static void EnableEdgeResize(Window window)
    {
        window.AddHandler(InputElement.PointerPressedEvent,(_,args)=>
        {
            if(window.WindowState!=WindowState.Normal || !args.GetCurrentPoint(window).Properties.IsLeftButtonPressed)return;
            var point=args.GetPosition(window);const double grip=5;
            var left=point.X<grip;var right=point.X>window.Bounds.Width-grip;var top=point.Y<grip;var bottom=point.Y>window.Bounds.Height-grip;
            WindowEdge? edge=left&&top?WindowEdge.NorthWest:right&&top?WindowEdge.NorthEast:left&&bottom?WindowEdge.SouthWest:right&&bottom?WindowEdge.SouthEast:
                left?WindowEdge.West:right?WindowEdge.East:top?WindowEdge.North:bottom?WindowEdge.South:null;
            if(edge is {} value){window.BeginResizeDrag(value,args);args.Handled=true;}
        },RoutingStrategies.Tunnel);
    }
}
