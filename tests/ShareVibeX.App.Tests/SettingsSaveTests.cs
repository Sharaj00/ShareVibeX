using Avalonia.LogicalTree;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Settings;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using ShareVibeX.Core.Localization;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class SettingsSaveTests(UiTestFixture ui)
{
    [Theory]
    [InlineData(false,"Ctrl+C")]
    [InlineData(true,"Alt+F8")]
    public Task ShortcutUsesPhysicalKeyAndEscapePreservesPreviousValue(bool cancel,string expected)=>ui.Run(async()=>
    {
        var fluent=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(fluent);
        var saved=new TaskCompletionSource<AppSettings>();
        var window=new SettingsWindow(new AppSettings(CaptureHotkey:"Alt+F8"),
            value=>{saved.SetResult(value);return Task.CompletedTask;},_=>{});
        try
        {
            window.Show();
            window.UpdateLayout();
            var change=window.GetVisualDescendants().OfType<Button>()
                .Single(control=>AutomationProperties.GetName(control)==Loc.T("Change shortcut"));
            Assert.Equal("Alt+F8",change.Content);
            change.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(cancel)window.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,null);
            window.KeyPress(Key.None,RawInputModifiers.Control,PhysicalKey.C,"с");
            window.GetVisualDescendants().OfType<Button>()
                .Single(control=>AutomationProperties.GetName(control)=="Save settings")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(expected,(await saved.Task.WaitAsync(TimeSpan.FromSeconds(3))).CaptureHotkey);
        }
        finally{window.Close();Avalonia.Application.Current!.Styles.Remove(fluent);}
    });

    [Fact]
    public Task VideoDraftIsSavedWithExistingScreenshotSettings()=>ui.Run(async()=>
    {
        var saved=new TaskCompletionSource<AppSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fluent=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(fluent);
        var settings=new AppSettings(Theme:ThemeMode.Contrast,CaptureHotkey:"Alt+F8",ImageFormat:ImageFormat.Webp,ImageQuality:72);
        var window=new SettingsWindow(settings,value=>{saved.SetResult(value);return Task.CompletedTask;},_=>{});
        try
        {
            window.Show();
            window.GetVisualDescendants().OfType<TabControl>().Single(control=>AutomationProperties.GetName(control)=="Settings sections").SelectedIndex=1;
            window.UpdateLayout();
            var fps=window.GetVisualDescendants().OfType<NumericUpDown>().Single(control=>AutomationProperties.GetName(control)=="Frame rate");
            fps.Value=60;
            window.GetVisualDescendants().OfType<Button>().Single(control=>Equals(control.Content,"Save settings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var result=await saved.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(60,result.RecordingDefaults.FrameRate);Assert.Equal(72,result.ImageQuality);
            Assert.Equal(ImageFormat.Webp,result.ImageFormat);Assert.Equal("Alt+F8",result.CaptureHotkey);Assert.Equal(ThemeMode.Contrast,result.Theme);
        }
        finally{window.Close();Avalonia.Application.Current!.Styles.Remove(fluent);}
    });
    [Fact]
    public Task PendingSaveCannotBeClosedAndReopenedWithStaleSettings()=>ui.Run(async()=>
    {
        var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var window=new SettingsWindow(new AppSettings(),_=>pending.Task,_=>{});
        window.Closed+=(_,_)=>closed.TrySetResult();
        try
        {
            window.Show();
            var button=window.GetVisualDescendants().OfType<Button>().Single(control=>Equals(control.Content,"Save settings"));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.Close();Assert.True(window.IsVisible);
            pending.TrySetResult();await closed.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(window.IsVisible);
        }
        finally{pending.TrySetResult();window.Close();}
    });
    [Fact]
    public Task HistoryControlsSaveAndNavigationIncludesCapture()=>ui.Run(async()=>
    {
        var saved=new TaskCompletionSource<AppSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fluent=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(fluent);
        var window=new SettingsWindow(new AppSettings(),value=>{saved.SetResult(value);return Task.CompletedTask;},_=>{});
        try
        {
            window.Show();window.UpdateLayout();
            var sections=window.GetVisualDescendants().OfType<TabControl>().Single(c=>AutomationProperties.GetName(c)=="Settings sections");
            Assert.Equal(3,sections.ItemCount);
            var enabled=window.GetVisualDescendants().OfType<CheckBox>().Single(c=>AutomationProperties.GetName(c)=="Enable capture history");
            Assert.False(enabled.IsChecked);enabled.IsChecked=true;
            var folder=window.GetVisualDescendants().OfType<TextBox>().Single(c=>AutomationProperties.GetName(c)=="Output folder");
            Assert.Equal(AppSettings.DefaultCaptureDirectory,folder.Text);folder.Text=@"D:\Captures";
            window.GetVisualDescendants().OfType<Button>().Single(c=>Equals(c.Content,"Save settings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var result=await saved.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.True(result.HistoryEnabled);Assert.Equal(@"D:\Captures",result.OutputDirectory);
        }
        finally{window.Close();Avalonia.Application.Current!.Styles.Remove(fluent);}
    });
    [Fact]
    public Task RemovedPreferencesAreAbsentAndResetOnSave()=>ui.Run(async()=>
    {
        var saved=new TaskCompletionSource<AppSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window=new SettingsWindow(new AppSettings(ButtonSize:64,OpenSettingsOnStartup:true),value=>{saved.SetResult(value);return Task.CompletedTask;},_=>{});
        try
        {
            window.Show();window.UpdateLayout();
            Assert.DoesNotContain(window.GetVisualDescendants(),c=>AutomationProperties.GetName(c) is "Button size" or "Open settings on startup");
            window.GetVisualDescendants().OfType<Button>().Single(c=>Equals(c.Content,"Save settings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var result=await saved.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.Equal(40,result.ButtonSize);Assert.False(result.OpenSettingsOnStartup);
        }
        finally{window.Close();}
    });
    [Fact]
    public Task CompletionControlsAreOptInAndSaveTheirValues()=>ui.Run(async()=>
    {
        var fluent=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(fluent);
        var saved=new TaskCompletionSource<AppSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window=new SettingsWindow(new AppSettings(),value=>{saved.SetResult(value);return Task.CompletedTask;},_=>{});
        try
        {
            window.Show();window.UpdateLayout();
            var checks=window.GetLogicalDescendants().OfType<CheckBox>().Distinct().ToArray();
            var sound=checks.Single(c=>AutomationProperties.GetName(c)=="Play completion sound");var notice=checks.Single(c=>AutomationProperties.GetName(c)=="Show completion notifications");
            var tabs=window.GetLogicalDescendants().OfType<TabControl>().Single(c=>AutomationProperties.GetName(c)=="Settings sections");
            var general=(Control)((ScrollViewer)((TabItem)tabs.SelectedItem!).Content!).Content!;
            var options=general.GetLogicalDescendants().OfType<StackPanel>().Single(c=>AutomationProperties.GetName(c)=="Notification options");
            Assert.False(sound.IsChecked);Assert.False(notice.IsChecked);Assert.False(options.IsVisible);
            sound.IsChecked=notice.IsChecked=true;Assert.True(options.IsVisible);
            notice.IsChecked=false;Assert.False(options.IsVisible);notice.IsChecked=true;
            window.GetLogicalDescendants().OfType<TextBox>().Distinct().Single(c=>AutomationProperties.GetName(c)=="Custom completion sound").Text="sounds/done.wav";
            window.GetLogicalDescendants().OfType<ComboBox>().Distinct().Single(c=>AutomationProperties.GetName(c)=="Notification middle action").SelectedItem=CompletionClickAction.OpenFolder;
            window.GetLogicalDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Save settings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var result=await saved.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.True(result.CompletionDefaults.PlaySound);Assert.True(result.CompletionDefaults.ShowNotifications);Assert.Equal("sounds/done.wav",result.CompletionDefaults.CustomSoundPath);Assert.Equal(CompletionClickAction.OpenFolder,result.CompletionDefaults.MiddleAction);
        }
        finally{window.Close();Avalonia.Application.Current!.Styles.Remove(fluent);}
    });
}
