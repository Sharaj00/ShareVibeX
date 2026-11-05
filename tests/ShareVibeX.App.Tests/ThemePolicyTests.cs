using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using ShareVibeX.App.Themes;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class ThemePolicyTests(UiTestFixture ui)
{
    [Fact]
    public Task SystemDarkUsesWindows11SurfacesEvenWithFluentLoaded()=>ui.Run(()=>
    {
        var app=Application.Current!;var fluent=new FluentTheme();app.Styles.Add(fluent);
        try
        {
            ThemeManager.ApplyPlatformColors(app,ThemeMode.System,new PlatformColorValues{ThemeVariant=PlatformThemeVariant.Dark,AccentColor1=Colors.Cyan});
            Assert.Equal(Color.Parse("#202020"),((ISolidColorBrush)app.Resources["WindowBackground"]!).Color);
            Assert.Equal(Color.Parse("#2B2B2B"),((ISolidColorBrush)app.Resources["CardBackground"]!).Color);
        }
        finally{app.Styles.Remove(fluent);}
    });
    [Fact]
    public Task SystemThemeTracksPlatformModeAndAccent()=>ui.Run(()=>
    {
        var app=Application.Current!;var accent=Color.Parse("#FFAA44");
        ThemeManager.ApplyPlatformColors(app,ThemeMode.System,new PlatformColorValues{ThemeVariant=PlatformThemeVariant.Light,AccentColor1=accent});
        Assert.Equal(ThemeVariant.Light,app.RequestedThemeVariant);Assert.Equal(accent,app.Resources["SystemAccentColor"]);
        var light=((ISolidColorBrush)app.Resources["WindowBackground"]!).Color;
        ThemeManager.ApplyPlatformColors(app,ThemeMode.System,new PlatformColorValues{ThemeVariant=PlatformThemeVariant.Dark,AccentColor1=Colors.Purple});
        Assert.Equal(ThemeVariant.Dark,app.RequestedThemeVariant);Assert.Equal(Colors.Purple,app.Resources["SystemAccentColor"]);
        Assert.NotEqual(light,((ISolidColorBrush)app.Resources["WindowBackground"]!).Color);
    });
    [Fact]
    public Task ContrastRemainsBlackWhenWindowsIsLight()=>ui.Run(()=>
    {
        var app=Application.Current!;
        ThemeManager.ApplyPlatformColors(app,ThemeMode.Contrast,new PlatformColorValues{ThemeVariant=PlatformThemeVariant.Light,AccentColor1=Colors.Cyan});
        Assert.Equal(ThemeVariant.Dark,app.RequestedThemeVariant);
        Assert.Equal(Colors.Black,((ISolidColorBrush)app.Resources["WindowBackground"]!).Color);
        Assert.Equal(Colors.Black,((ISolidColorBrush)app.Resources["CardBackground"]!).Color);
        Assert.Equal(Colors.Cyan,app.Resources["SystemAccentColor"]);
    });
}
