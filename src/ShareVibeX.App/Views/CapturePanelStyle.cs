using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;

namespace ShareVibeX.App.Views;

internal static class CapturePanelStyle
{
    public static int ButtonSize {get;set;}=40;
    public static void Apply(Button button)
    {
        // Only explicitly registered capture actions use these resources; palette popups and Settings do not.
        button.Classes.Add("capture-action");var type=button.GetType();
        Style Rule(string? state,bool template=false)
        {
            var style=new Style(selector=>
            {
                var result=selector.OfType(type);if(state is not null)result=result.Class(state);
                return template?result.Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"):result;
            });button.Styles.Add(style);return style;
        }
        void Background(string? state,string resource)
        {
            Rule(state).Setters.Add(new Setter(TemplatedControl.BackgroundProperty,new DynamicResourceExtension(resource)));
            var part=Rule(state,true);part.Setters.Add(new Setter(TemplatedControl.BackgroundProperty,new DynamicResourceExtension(resource)));
            part.Setters.Add(new Setter(TemplatedControl.ForegroundProperty,new DynamicResourceExtension("CaptureButtonForeground")));
        }
        Background(null,"CaptureButtonBackground");Background(":pointerover","CaptureButtonHover");Background(":pressed","CaptureButtonPressed");
        foreach(var state in new[]{"accent",":checked"})
        {
            Background(state,"CaptureButtonSelected");
            foreach(var template in new[]{false,true})
            {
                var selected=Rule(state,template);
                selected.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(1)));
                selected.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty,new DynamicResourceExtension("AccentBrush")));
            }
        }
        Background(":disabled","CaptureButtonBackground");Rule(":disabled").Setters.Add(new Setter(Visual.OpacityProperty,0.45));
        Rule(null).Setters.Add(new Setter(TemplatedControl.ForegroundProperty,new DynamicResourceExtension("CaptureButtonForeground")));
    }
    public static void Apply(Window window)
    {
        window.Background=Brushes.Transparent;window.TransparencyLevelHint=[WindowTransparencyLevel.Transparent];window.TransparencyBackgroundFallback=Brushes.Transparent;
    }
    public static void Apply(Border border)
    {border.Background=Brushes.Transparent;border.BorderThickness=new Thickness(0);border.Padding=new Thickness(8);border.CornerRadius=new CornerRadius(8);}
}

