using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Localization;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class LanguageSettingsTests(UiTestFixture ui)
{
    [Fact]
    public Task RussianSettingsTranslateAndSaveLanguageWithoutChangingUserData()=>ui.Run(async()=>
    {
        var previous=Loc.Language;Loc.Language=AppLanguage.Russian;
        var fluent=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(fluent);
        AppSettings? saved=null;var settings=new AppSettings(OutputDirectory:@"C:\User\My Captures",CaptureHotkey:"Alt+F8",Language:AppLanguage.Russian);
        var window=new SettingsWindow(settings,value=>{saved=value;return Task.CompletedTask;},_=>{});
        try
        {
            window.Show();window.UpdateLayout();
            Assert.Contains(window.GetLogicalDescendants().OfType<TabItem>(),t=>Equals(t.Header,"Общее"));
            var language=window.GetLogicalDescendants().OfType<ComboBox>().Distinct().Single(c=>AutomationProperties.GetName(c)=="Interface language");
            Assert.Equal(1,language.SelectedIndex);language.SelectedIndex=0;
            window.GetLogicalDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Сохранить настройки")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for(var i=0;i<20&&saved is null;i++)await Task.Delay(10);
            Assert.NotNull(saved);Assert.Equal(AppLanguage.English,saved.Language);Assert.Equal(settings.OutputDirectory,saved.OutputDirectory);Assert.Equal(settings.CaptureHotkey,saved.CaptureHotkey);
        }
        finally{window.Close();Loc.Language=previous;Avalonia.Application.Current!.Styles.Remove(fluent);}
    });
    [Fact]
    public Task PointerSavePersistsRussianAndReopenedSettingsUseIt()=>ui.Run(async()=>
    {
        var previous=Loc.Language;Loc.Language=AppLanguage.English;
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-language-"+Guid.NewGuid().ToString("N"));
        var store=new PortableSettingsStore(root);var closed=new TaskCompletionSource();
        var fluent=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(fluent);
        var window=new SettingsWindow(new AppSettings(),async value=>{await ShareVibeX.App.Startup.SettingsUpdate.SaveAsync(new AppSettings(),new AppSettings(),value,_=>throw new Exception("Unavailable hotkey"),s=>store.SaveAsync(s),(_,_)=>throw new Exception("Unrelated autostart error"));Loc.Language=value.Language;},_=>{});
        window.Closed+=(_,_)=>closed.TrySetResult();SettingsWindow? reopened=null;
        try
        {
            window.Show();window.UpdateLayout();
            var language=window.GetLogicalDescendants().OfType<ComboBox>().Distinct().Single(c=>AutomationProperties.GetName(c)=="Interface language");language.SelectedIndex=1;
            var button=window.GetLogicalDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Save settings"));
            var point=button.TranslatePoint(new Point(button.Bounds.Width/2,button.Bounds.Height/2),window)!.Value;
            window.MouseDown(point,MouseButton.Left);window.MouseUp(point,MouseButton.Left);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(3));var loaded=await store.LoadAsync();Assert.Equal(AppLanguage.Russian,loaded.Language);
            reopened=new SettingsWindow(loaded,_=>Task.CompletedTask,_=>{});reopened.Show();reopened.UpdateLayout();
            Assert.Contains(reopened.GetLogicalDescendants().OfType<TabItem>(),t=>Equals(t.Header,"Общее"));
        }
        finally{window.Close();reopened?.Close();Loc.Language=previous;Avalonia.Application.Current!.Styles.Remove(fluent);if(Directory.Exists(root))Directory.Delete(root,true);}
    });
}
