using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Views;

public sealed class CaptureSettingsPage:UserControl
{
    internal event Action? SettingsChanged;
    private readonly CheckBox windows,controls,restrict,position,size,crosshair,center;
    private readonly NumericUpDown dim,opacity,thickness;
    private readonly ComboBox outlineStyle;
    private readonly ColorPicker outlineColor;
    private readonly CheckBox showOutline;
    private readonly TextBox template,presets;
    public CaptureSettingsPage(SelectionOptions options)
    {
        CheckBox Toggle(string name,bool value){var box=new CheckBox{Content=Loc.T(name),IsChecked=value};AutomationProperties.SetName(box,name);return box;}
        windows=Toggle("Detect window regions",options.DetectWindows);controls=Toggle("Detect child controls",options.DetectControls);
        controls.IsEnabled=options.DetectWindows;windows.PropertyChanged+=(_,e)=>{if(e.Property==Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty)controls.IsEnabled=windows.IsChecked==true;};
        restrict=Toggle("Restrict to active monitor",options.RestrictToActiveMonitor);
        position=Toggle("Show selection position",options.ShowPosition);size=Toggle("Show selection size",options.ShowSize);
        crosshair=Toggle("Screen-wide crosshair",options.ScreenCrosshair);center=Toggle("Selection center crosshair",options.CenterCrosshair);
        dim=new NumericUpDown{Minimum=0,Maximum=90,Increment=1,Value=options.EditorDimPercent};AutomationProperties.SetName(dim,"Editor background dim percent");
        template=new TextBox{Text=options.InfoTemplate,AcceptsReturn=true,MinHeight=70,TextWrapping=Avalonia.Media.TextWrapping.Wrap};AutomationProperties.SetName(template,"Selection information template");
        presets=new TextBox{Text=options.SnapSizes,AcceptsReturn=true,MinHeight=90,TextWrapping=Avalonia.Media.TextWrapping.Wrap};AutomationProperties.SetName(presets,"Selection snap sizes");
        ToolTip.SetTip(windows,Loc.T("Hover to inspect an external window; click to select it or drag for a custom region."));
        ToolTip.SetTip(restrict,Loc.T("The active monitor is chosen from the cursor position when capture starts. Cursor confinement is released on every exit."));
        ToolTip.SetTip(controls,Loc.T("Detect selectable controls inside the hovered window."));
        ToolTip.SetTip(position,Loc.T("Show the X and Y coordinates of the selected region."));
        ToolTip.SetTip(size,Loc.T("Show the width and height of the selected region."));
        ToolTip.SetTip(crosshair,Loc.T("Draw guide lines across the monitor at the pointer."));
        ToolTip.SetTip(center,Loc.T("Mark the center of the selected region."));
        ToolTip.SetTip(dim,Loc.T("Live selection remains undimmed."));
        ToolTip.SetTip(template,Loc.T("Tokens: {x}, {y}, {width}, {height}, {newline}. No pixel sampling."));
        var selection=Ui.Stack(windows,controls,restrict);
        var overlays=Ui.Stack(Ui.Text("Frozen editor dim (%)",14),dim,position,size,crosshair,center,Ui.Text("Information template",14),template);
        ToolTip.SetTip(presets,Loc.T("Physical pixels, separated by semicolons: 640x480;1280x720. Edit this list to add or remove sizes. Shift takes priority and draws a square."));
        var sizes=Ui.Stack(Ui.Text("Alt-drag snap sizes",16),presets);
        outlineStyle=new ComboBox{ItemsSource=Enum.GetValues<CaptureOutlineStyle>(),SelectedItem=options.OutlineStyle,
            ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<CaptureOutlineStyle>((style,_)=>Ui.Text(style.ToString()))};
        outlineColor=new ColorPicker{Color=Color.Parse(options.OutlineColor),IsAlphaEnabled=false};
        AutomationProperties.SetName(outlineColor,"Frame color");
        opacity=new NumericUpDown{Minimum=0,Maximum=100,Increment=5,Value=options.OutlineOpacity};
        thickness=new NumericUpDown{Minimum=1,Maximum=12,Increment=1,Value=options.OutlineThickness};
        AutomationProperties.SetName(thickness,"Frame thickness (px)");
        var preview=new FramePreview{Width=76,Height=28,VerticalAlignment=VerticalAlignment.Center};
        AutomationProperties.SetName(preview,"Frame preview");
        var styleAndPreview=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=10,Children={outlineStyle,preview}};
        Grid.SetColumn(preview,1);
        void UpdatePreview()
        {
            var frame=FrameOptions();
            preview.Options=frame;
            preview.InvalidateVisual();
        }
        UpdatePreview();
        outlineColor.ColorChanged+=(_,_)=>{UpdatePreview();SettingsChanged?.Invoke();};
        opacity.ValueChanged+=(_,_)=>UpdatePreview();
        thickness.ValueChanged+=(_,_)=>UpdatePreview();
        outlineStyle.SelectionChanged+=(_,_)=>UpdatePreview();
        showOutline=Toggle("Show frame while recording",options.ShowRecordingOutline);
        var outline=Ui.Stack(
            SettingsWindow.Row("Frame color","Open color palette",outlineColor),
            SettingsWindow.Row("Frame line","Choose a dashed, solid or dotted line.",styleAndPreview),
            SettingsWindow.Row("Frame thickness (px)","Set the capture outline thickness in screen pixels.",thickness),
            SettingsWindow.Row("Frame opacity (%)","0 hides the line and 100 makes it opaque.",opacity),showOutline);
        var tabs=new TabControl{SelectedIndex=0,ItemsSource=new[]{new TabItem{Header=Ui.Text("Selection",14),Content=selection},new TabItem{Header=Ui.Text("Overlays",14),Content=overlays},new TabItem{Header=Ui.Text("Snap sizes",14),Content=sizes},new TabItem{Header=Ui.Text("Frame",14),Content=outline}}};
        Content=tabs;
        foreach(var check in new[]{windows,controls,restrict,position,size,crosshair,center,showOutline})
            check.IsCheckedChanged+=(_,_)=>SettingsChanged?.Invoke();
        foreach(var number in new[]{dim,opacity,thickness})
            number.ValueChanged+=(_,_)=>{if(number.Value is not null)SettingsChanged?.Invoke();};
        foreach(var input in new[]{template,presets})
            input.PropertyChanged+=(_,e)=>{if(e.Property==TextBox.TextProperty)SettingsChanged?.Invoke();};
        outlineStyle.SelectionChanged+=(_,_)=>{if(outlineStyle.SelectedItem is not null)SettingsChanged?.Invoke();};
    }
    private SelectionOptions FrameOptions()=>new()
    {
        OutlineStyle=outlineStyle.SelectedItem is CaptureOutlineStyle style?style:CaptureOutlineStyle.Dashed,
        OutlineColor=$"#{outlineColor.Color.R:X2}{outlineColor.Color.G:X2}{outlineColor.Color.B:X2}",
        OutlineOpacity=(int)(opacity.Value??100),
        OutlineThickness=(int)(thickness.Value??1)
    };

    public SelectionOptions GetOptions()
    {
        var result=FrameOptions() with{ShowRecordingOutline=showOutline.IsChecked==true,DetectWindows=windows.IsChecked==true,DetectControls=controls.IsChecked==true,RestrictToActiveMonitor=restrict.IsChecked==true,ShowPosition=position.IsChecked==true,ShowSize=size.IsChecked==true,ScreenCrosshair=crosshair.IsChecked==true,CenterCrosshair=center.IsChecked==true,EditorDimPercent=(int)(dim.Value??53),InfoTemplate=template.Text??"",SnapSizes=presets.Text??""};
        ShareVibeX.Core.Capture.SelectionRules.Validate(result);return result;
    }
    private sealed class FramePreview:Control
    {
        public SelectionOptions Options {get;set;}=new();
        public override void Render(DrawingContext context)
        {
            var pen=CaptureOutline.CreatePen(Options,TopLevel.GetTopLevel(this)?.RenderScaling??1);
            var inset=pen.Thickness/2;
            context.DrawLine(pen,new Point(inset,Bounds.Height/2),new Point(Bounds.Width-inset,Bounds.Height/2));
        }
    }
}
