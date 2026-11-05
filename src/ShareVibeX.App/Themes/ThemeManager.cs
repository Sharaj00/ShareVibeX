using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using ShareVibeX.Core.Settings;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using System.Runtime.CompilerServices;

namespace ShareVibeX.App.Themes;

public static class ThemeManager
{
    private static readonly ConditionalWeakTable<Application,Subscription> Subscriptions=new();
    public static void ApplyPlatformColors(Application app,ThemeMode mode,PlatformColorValues values)
    {
        var contrast=mode==ThemeMode.Contrast;
        var light=!contrast && values.ThemeVariant!=PlatformThemeVariant.Dark;
        var variant=light?ThemeVariant.Light:ThemeVariant.Dark;
        app.RequestedThemeVariant=variant;
        var accent=values.AccentColor1;
        app.Resources["SystemAccentColor"]=accent;
        app.Resources["AccentBrush"]=new SolidColorBrush(accent);
        foreach(var theme in app.Styles.OfType<FluentTheme>())
            foreach(var palette in theme.Palettes.Values)palette.Accent=accent;
        // Fluent's SystemRegionBrush is black in dark mode, unlike Windows 11 Settings surfaces.
        app.Resources["WindowBackground"]=Brush.Parse(contrast?"#000000":light?"#F3F3F3":"#202020");
        app.Resources["CardBackground"]=Brush.Parse(contrast?"#000000":light?"#FBFBFB":"#2B2B2B");
        app.Resources["CardBorder"]=Brush.Parse(contrast?"#505050":light?"#E5E5E5":"#303030");
        app.Resources["ControlBackground"]=Brush.Parse(contrast?"#101010":light?"#FFFFFF":"#373737");
        app.Resources["ControlBorder"]=Brush.Parse(contrast?"#606060":light?"#D5D5D5":"#454545");
        app.Resources["NavigationSelection"]=Brush.Parse(contrast?"#171717":light?"#E5E5E5":"#333333");
        app.Resources["CaptureButtonBackground"]=Brush.Parse(contrast?"#B3000000":light?"#F0F0F0":"#373737");
        app.Resources["CaptureButtonHover"]=Brush.Parse(contrast?"#D9000000":light?"#E2E2E2":"#454545");
        app.Resources["CaptureButtonPressed"]=Brush.Parse(contrast?"#EB000000":light?"#D5D5D5":"#505050");
        app.Resources["CaptureButtonSelected"]=contrast?Brush.Parse("#D9000000"):new SolidColorBrush(accent);
        app.Resources["CaptureButtonForeground"]=light?Brushes.Black:Brushes.White;
    }
    public static void Apply(Application app, ThemeMode theme)
    {
        var subscription=Subscriptions.GetValue(app,key=>new Subscription(key));subscription.Mode=theme;
        ApplyPlatformColors(app,theme,app.PlatformSettings?.GetColorValues()??new PlatformColorValues());
    }
    public static void Detach(Application app){if(Subscriptions.TryGetValue(app,out var subscription))subscription.Dispose();Subscriptions.Remove(app);}
    private sealed class Subscription:IDisposable
    {
        private readonly Application app;private readonly IPlatformSettings? platform;
        public ThemeMode Mode;private bool disposed;
        public Subscription(Application app){this.app=app;platform=app.PlatformSettings;if(platform is not null)platform.ColorValuesChanged+=Changed;}
        private void Changed(object? sender,PlatformColorValues values)=>Dispatcher.UIThread.Post(()=>{if(!disposed)ApplyPlatformColors(app,Mode,values);});
        public void Dispose(){disposed=true;if(platform is not null)platform.ColorValuesChanged-=Changed;}
    }
}
