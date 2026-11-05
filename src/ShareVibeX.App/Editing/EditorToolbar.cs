using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.App.Editing;

public sealed class EditorToolbar:Border
{
    private readonly TextBlock info=Ui.Text("",12);
    private readonly Dictionary<EditorTool,Button> tools=[];
    private readonly ComboBox effect;
    private readonly ColorPicker color;
    private readonly WheelThickness width;
    private bool syncing;
    public TextBlock Status {get;}=Ui.Text("",12);
    public EditorToolbar(CaptureEditorState state,Action refresh,Func<bool,Task> export,Action cancel)
    {
        Classes.Add("card");CapturePanelStyle.Apply(this);Padding=new Thickness(8);CornerRadius=new CornerRadius(8);
        var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        void Tool(EditorTool value,string geometry,string help)
        {
            var button=Ui.Button("",()=>{state.Tool=value;state.Draft=null;refresh();});CapturePanelStyle.Apply(button);button.Padding=new Thickness(10,6);button.Margin=new Thickness(2);
            button.Width=40;button.Height=40;button.Padding=new Thickness(8);button.Content=new PathIcon{Data=Geometry.Parse(geometry),Width=22,Height=22};AutomationProperties.SetName(button,value.ToString());ToolTip.SetTip(button,Loc.T(help));tools[value]=button;row.Children.Add(button);
        }
        Tool(EditorTool.Region,"F0 M3,3 L21,3 L21,21 L3,21 Z M5,5 L5,19 L19,19 L19,5 Z M8,8 L16,8 L16,16 L8,16 Z","Region · Tab: move / resize / reselect");
        Tool(EditorTool.Rectangle,"F0 M2,5 L22,5 L22,19 L2,19 Z M4,7 L4,17 L20,17 L20,7 Z","Rectangle · R");Tool(EditorTool.Ellipse,"F0 M12,2 A10,10 0 1 1 11.99,2 Z M12,4 A8,8 0 1 0 12.01,4 Z","Ellipse · E");Tool(EditorTool.Pen,"F0 M3,17 L15,5 L19,9 L7,21 L3,21 Z M16,4 L18,2 L22,6 L20,8 Z","Pen · F");
        Tool(EditorTool.Line,"F0 M3,19 L19,3 L21,5 L5,21 Z","Line · L");Tool(EditorTool.Arrow,"F0 M2,11 L16,11 L11,6 L13,4 L21,12 L13,20 L11,18 L16,13 L2,13 Z","Arrow · A");Tool(EditorTool.Text,"F0 M3,3 L21,3 L21,6 L14,6 L14,21 L10,21 L10,6 L3,6 Z","Text area · T: draw a box, type, resize to scale text");
        effect=new ComboBox{ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<ShapeEffect>((value,_)=>Ui.Text(value.ToString())),ItemsSource=Enum.GetValues<ShapeEffect>(),SelectedItem=state.Effect,Width=110,Height=40,Margin=new Thickness(4,2)};
        AutomationProperties.SetName(effect,"Shape effect");effect.SelectionChanged+=(_,_)=>{if(!syncing && effect.SelectedItem is ShapeEffect value){state.SetEffect(value);refresh();}};
        color=new ColorPicker{Color=Color.FromUInt32(state.Color),Width=52,MinWidth=52,Height=40,Margin=new Thickness(4,2)};
        AutomationProperties.SetName(color,"Annotation color palette");ToolTip.SetTip(color,Loc.T("Open color palette"));
        color.ColorChanged+=(_,args)=>{if(!syncing){state.SetColor(args.NewColor.ToUInt32());refresh();}};
        width=new WheelThickness(state,refresh){Width=48,Height=40,Margin=new Thickness(4,2)};
        row.Children.Add(effect);row.Children.Add(color);row.Children.Add(width);
        Button ExportIcon(string name,string hint,string data,bool save)
        {
            var button=Ui.Button("",async()=>await export(save),!save);CapturePanelStyle.Apply(button);
            button.Width=40;button.Height=40;button.Content=new PathIcon{Data=Geometry.Parse(data),Width=22,Height=22};button.Padding=new Thickness(8);button.Margin=new Thickness(2);
            AutomationProperties.SetName(button,name);ToolTip.SetTip(button,Loc.T(hint));return button;
        }
        row.Children.Add(ExportIcon("Copy screenshot","Copy · Ctrl+C / Enter","F0 M8,2 L22,2 L22,18 L18,18 L18,22 L2,22 L2,6 L8,6 Z M10,4 L10,6 L18,6 L18,16 L20,16 L20,4 Z M4,8 L4,20 L16,20 L16,8 Z",false));
        row.Children.Add(ExportIcon("Save screenshot","Save · Ctrl+S","F0 M2,2 L18,2 L22,6 L22,22 L2,22 Z M4,4 L4,20 L6,20 L6,12 L18,12 L18,20 L20,20 L20,7 L17,4 L16,4 L16,10 L6,10 L6,4 Z M8,4 L8,8 L14,8 L14,4 Z M8,14 L8,20 L16,20 L16,14 Z",true));
        var close=Ui.Button("",cancel);CapturePanelStyle.Apply(close);close.Width=40;close.Height=40;close.Padding=new Thickness(8);close.Margin=new Thickness(2);close.Content=new PathIcon{Data=Geometry.Parse("M4,6 L6,4 L12,10 L18,4 L20,6 L14,12 L20,18 L18,20 L12,14 L6,20 L4,18 L10,12 Z"),Width=22,Height=22};AutomationProperties.SetName(close,"Cancel screenshot");ToolTip.SetTip(close,Loc.T("Cancel · Esc"));row.Children.Add(close);
        Child=Ui.Stack(new ScrollViewer{Content=row,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled},info,Status);
    }
    public void Update(CaptureEditorState state)
    {
        IsEnabled=!state.IsExporting;syncing=true;
        try{effect.SelectedItem=state.Effect;color.Color=Color.FromUInt32(state.Color);width.UpdateValue();}
        finally{syncing=false;}
        effect.IsEnabled=state.Tool is EditorTool.Rectangle or EditorTool.Ellipse;
        width.IsEnabled=state.Tool is not (EditorTool.Text or EditorTool.Region);
        foreach(var pair in tools)pair.Value.Classes.Set("accent",pair.Key==state.Tool);
        info.Text=$"{state.Region.Width} × {state.Region.Height} px";
        ToolTip.SetTip(info,Loc.T("Ctrl+drag: move · Wheel: width · Ctrl+Z / Ctrl+Shift+Z · Esc: cancel"));
    }
}




