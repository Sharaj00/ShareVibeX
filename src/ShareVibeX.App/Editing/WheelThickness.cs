using Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ShareVibeX.App.Views;

namespace ShareVibeX.App.Editing;

public sealed class WheelThickness:Border
{
    static WheelThickness()=>BackgroundProperty.OverrideDefaultValue<WheelThickness>(Brush.Parse("#303030"));
    private readonly CaptureEditorState state;
    private readonly Action changed;
    private readonly TextBlock number=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,FontSize=15};
    public WheelThickness(CaptureEditorState state,Action changed)
    {
        this.state=state;this.changed=changed;Focusable=true;MinWidth=48;Height=34;Padding=new Thickness(10,4);CornerRadius=new CornerRadius(4);
        this.Bind(BackgroundProperty,this.GetResourceObservable("CaptureButtonBackground"));number.Bind(TextBlock.ForegroundProperty,number.GetResourceObservable("CaptureButtonForeground"));Child=number;ToolTip.SetTip(this,Loc.T("Width / blur strength · mouse wheel or ↑ / ↓"));UpdateValue();
    }
    public void UpdateValue()
    {
        number.Text=state.Width.ToString();AutomationProperties.SetName(this,$"Width / blur strength: {state.Width}");
    }
    private void Change(double delta){if(!state.IsExporting && state.AdjustThickness(delta)){UpdateValue();changed();}}
    protected override void OnPointerWheelChanged(PointerWheelEventArgs args){Change(args.Delta.Y);args.Handled=true;}
    protected override void OnKeyDown(KeyEventArgs args)
    {
        if(args.Key is Key.Up or Key.Down){Change(args.Key==Key.Up?1:-1);args.Handled=true;}else base.OnKeyDown(args);
    }
}
