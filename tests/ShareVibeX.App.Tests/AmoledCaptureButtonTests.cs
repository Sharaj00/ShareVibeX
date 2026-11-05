using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using ShareVibeX.App.Editing;
using ShareVibeX.App.Themes;
using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class AmoledCaptureButtonTests(UiTestFixture ui)
{
    [Fact]
    public Task CaptureButtonsStayTranslucentBlackWithVisibleSelectedState()=>ui.Run(()=>
    {
        var app=Application.Current!;var fluent=new Avalonia.Themes.Fluent.FluentTheme();app.Styles.Add(fluent);
        var state=new CaptureEditorState();var toolbar=new EditorToolbar(state,()=>{},_=>Task.CompletedTask,()=>{});
        var window=new Window{Width=1000,Height=180,Content=toolbar};
        try
        {
            ThemeManager.ApplyPlatformColors(app,ThemeMode.Contrast,new PlatformColorValues{ThemeVariant=PlatformThemeVariant.Dark,AccentColor1=Colors.Cyan});
            window.Show();toolbar.Update(state);window.UpdateLayout();
            var rectangle=toolbar.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Rectangle");
            var color=Assert.IsAssignableFrom<ISolidColorBrush>(rectangle.Background).Color;
            Assert.Equal((byte)0,color.R);Assert.Equal((byte)0,color.G);Assert.Equal((byte)0,color.B);Assert.InRange(color.A,(byte)1,(byte)254);
            state.Tool=EditorTool.Rectangle;toolbar.Update(state);window.UpdateLayout();
            Assert.Equal(Colors.Cyan,Assert.IsAssignableFrom<ISolidColorBrush>(rectangle.BorderBrush).Color);
            Assert.True(rectangle.BorderThickness.Left>0);
            color=Assert.IsAssignableFrom<ISolidColorBrush>(rectangle.Background).Color;Assert.Equal((byte)0,color.R);Assert.InRange(color.A,(byte)1,(byte)254);
            window.MouseMove(rectangle.TranslatePoint(new Point(12,12),window)!.Value);window.UpdateLayout();
            var presenter=rectangle.GetVisualDescendants().OfType<ContentPresenter>().Single(p=>p.Name=="PART_ContentPresenter");
            color=Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;Assert.Equal((byte)0,color.R);Assert.Equal((byte)0,color.G);Assert.Equal((byte)0,color.B);Assert.InRange(color.A,(byte)1,(byte)254);
            rectangle.IsEnabled=false;window.UpdateLayout();
            color=Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;Assert.Equal((byte)0,color.R);Assert.InRange(color.A,(byte)1,(byte)254);Assert.InRange(rectangle.Opacity,0.1,0.9);
            rectangle.IsEnabled=true;
            ThemeManager.ApplyPlatformColors(app,ThemeMode.System,new PlatformColorValues{ThemeVariant=PlatformThemeVariant.Dark});window.UpdateLayout();
            state.Tool=EditorTool.Region;toolbar.Update(state);window.UpdateLayout();
            color=Assert.IsAssignableFrom<ISolidColorBrush>(rectangle.Background).Color;Assert.Equal((byte)255,color.A);Assert.True(color.R>0);
        }
        finally{window.Close();app.Styles.Remove(fluent);ThemeManager.ApplyPlatformColors(app,ThemeMode.System,new PlatformColorValues{ThemeVariant=PlatformThemeVariant.Dark});}
    });
}
