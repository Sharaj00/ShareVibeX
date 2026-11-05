using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Views;

public sealed class CaptureSettingsPage:UserControl
{
    private readonly CheckBox windows,controls,restrict,position,size,crosshair,center;
    private readonly NumericUpDown dim,opacity;
    private readonly ComboBox outlineStyle;
    private readonly TextBox outlineColor;
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
        var selection=Ui.Stack(windows,controls,Ui.Text("Hover to inspect an external window; click to select it or drag for a custom region.",12,true),restrict,Ui.Text("The active monitor is chosen from the cursor position when capture starts. Cursor confinement is released on every exit.",12,true));
        var overlays=Ui.Stack(Ui.Text("Frozen editor dim (%)",14),dim,Ui.Text("Live selection remains undimmed.",12,true),position,size,crosshair,center,Ui.Text("Information template",14),template,Ui.Text("Tokens: {x}, {y}, {width}, {height}, {newline}. No pixel sampling.",12,true));
        var sizes=Ui.Stack(Ui.Text("Alt-drag snap sizes",16),presets,Ui.Text("Physical pixels, separated by semicolons: 640x480;1280x720. Edit this list to add or remove sizes. Shift takes priority and draws a square.",12,true));
        outlineStyle=new ComboBox{ItemsSource=Enum.GetValues<CaptureOutlineStyle>(),SelectedItem=options.OutlineStyle,
            ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<CaptureOutlineStyle>((style,_)=>Ui.Text(style.ToString()))};
        outlineColor=new TextBox{Text=options.OutlineColor,MaxLength=7};
        opacity=new NumericUpDown{Minimum=0,Maximum=100,Increment=5,Value=options.OutlineOpacity};
        showOutline=Toggle("Show frame while recording",options.ShowRecordingOutline);
        var outline=Ui.Stack(
            SettingsWindow.Row("Frame color","Enter a color as #RRGGBB.",outlineColor),
            SettingsWindow.Row("Frame line","Choose a dashed, solid or dotted line.",outlineStyle),
            SettingsWindow.Row("Frame opacity (%)","0 hides the line and 100 makes it opaque.",opacity),showOutline);
        var tabs=new TabControl{SelectedIndex=0,ItemsSource=new[]{new TabItem{Header=Ui.Text("Selection",14),Content=selection},new TabItem{Header=Ui.Text("Overlays",14),Content=overlays},new TabItem{Header=Ui.Text("Snap sizes",14),Content=sizes},new TabItem{Header=Ui.Text("Frame",14),Content=outline}}};
        Content=tabs;
    }
    public SelectionOptions GetOptions()
    {
        var result=new SelectionOptions{OutlineStyle=(CaptureOutlineStyle)outlineStyle.SelectedItem!,OutlineColor=outlineColor.Text?.Trim()??"",OutlineOpacity=(int)(opacity.Value??100),ShowRecordingOutline=showOutline.IsChecked==true,DetectWindows=windows.IsChecked==true,DetectControls=controls.IsChecked==true,RestrictToActiveMonitor=restrict.IsChecked==true,ShowPosition=position.IsChecked==true,ShowSize=size.IsChecked==true,ScreenCrosshair=crosshair.IsChecked==true,CenterCrosshair=center.IsChecked==true,EditorDimPercent=(int)(dim.Value??53),InfoTemplate=template.Text??"",SnapSizes=presets.Text??""};
        ShareVibeX.Core.Capture.SelectionRules.Validate(result);return result;
    }
}
