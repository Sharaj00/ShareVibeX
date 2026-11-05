using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ShareVibeX.App.History;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Localization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class HistoryPageTests(UiTestFixture ui)
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task FileDisappearanceRefreshesHistoryEvenWithoutSuccessResult(bool errorAfterDelete)=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-reconcile-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"2026-11"));var path=Path.Combine(root,"2026-11","app_20261104_101010.png");await File.WriteAllTextAsync(path,"fixture");
            using var page=new CaptureHistoryPage(new(()=>new AppSettings(HistoryDirectory:root)),recycle:(file,_)=>
            {File.Delete(file);if(errorAfterDelete)throw new IOException("Late fixture error");return Task.FromResult(false);});
            await page.ActivateAsync();await page.RecycleFileAsync(path);
            Assert.DoesNotContain(page.GetLogicalDescendants().OfType<Button>(),b=>AutomationProperties.GetName(b)?.StartsWith("Open capture: ")==true);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Fact]
    public Task RightClickTargetsFileAndCrossDoesNotOpenIt()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-input-"+Guid.NewGuid().ToString("N"));Window? window=null;
        var theme=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(theme);
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"2026-11"));var path=Path.Combine(root,"2026-11","app_20261104_101010.png");await File.WriteAllTextAsync(path,"fixture");
            var menus=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);var opened=0;var recycled=0;
            using var page=new CaptureHistoryPage(new(()=>new AppSettings(HistoryDirectory:root)),open:_=>opened++,menu:(file,_,_,_)=>menus.TrySetResult(file),recycle:(_,_)=>{recycled++;return Task.FromResult(false);});
            window=new Window{Width=800,Height=600,Content=page};window.Show();await page.ActivateAsync();window.UpdateLayout();
            var fileButton=page.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)?.StartsWith("Open capture: ")==true);
            Assert.True(fileButton.Bounds.Width>0);Assert.True(fileButton.Bounds.Height>0);
            var position=fileButton.TranslatePoint(new Point(20,20),window)!.Value;
            window.MouseDown(position,MouseButton.Right);window.MouseUp(position,MouseButton.Right);
            Assert.Equal(path,await menus.Task.WaitAsync(TimeSpan.FromSeconds(2)));Assert.Equal(0,opened);
            await page.ActivateAsync();window.UpdateLayout();
            var cross=page.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)?.StartsWith("Delete capture: ")==true);
            position=cross.TranslatePoint(new Point(18,18),window)!.Value;
            window.MouseDown(position,MouseButton.Left);window.MouseUp(position,MouseButton.Left);
            Assert.Equal(1,recycled);Assert.Equal(0,opened);Assert.True(File.Exists(path));
        }
        finally{window?.Close();Avalonia.Application.Current!.Styles.Remove(theme);if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task FileClickAndRecycleUseExactFileAndOnlyRemoveSuccessfulDeletion(bool success) => ui.Run(async () =>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-page-"+Guid.NewGuid().ToString("N"));
        try
        {
            var month=Directory.CreateDirectory(Path.Combine(root,"2026-11")).FullName;
            var path=Path.Combine(month,"app_20261104_101010.png");await File.WriteAllTextAsync(path,"fixture");
            string? opened=null,recycled=null,menuPath=null;
            using var page=new CaptureHistoryPage(new(()=>new AppSettings(HistoryDirectory:root)),open:file=>opened=file,
                menu:(file,_,_,_)=>menuPath=file,recycle:(file,_)=>{recycled=file;if(success)File.Delete(file);return Task.FromResult(success);});
            await page.ActivateAsync();
            Assert.DoesNotContain(page.GetLogicalDescendants().OfType<Control>(),control=>control.Classes.Contains("card"));
            Assert.DoesNotContain(page.GetLogicalDescendants().OfType<Button>(),button=>button.Content is string caption&&caption is "Refresh" or "Recover interrupted recordings");
            var fileButton=page.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Open capture: "+Path.GetFileName(path));
            fileButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Equal(path,opened);
            await page.ShowFileMenuAsync(path,default);Assert.Equal(path,menuPath);
            await page.RecycleFileAsync(path);Assert.Equal(path,recycled);
            Assert.Equal(!success,File.Exists(path));
            Assert.Equal(success?0:1,page.GetLogicalDescendants().OfType<Button>().Count(b=>AutomationProperties.GetName(b)?.StartsWith("Open capture: ")==true));
            Assert.DoesNotContain(page.GetLogicalDescendants().OfType<Button>(),b=>b.Content is string text && text is "Open" or "Folder");
            page.Deactivate();Assert.Empty(page.GetLogicalDescendants().OfType<Expander>());
            await page.ActivateAsync();Assert.Equal(success?0:1,page.GetLogicalDescendants().OfType<Expander>().Count());
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Fact]
    public Task SettingsHistorySelectionWorksWhenOtherSettingsAreLocked() => ui.Run(() =>
    {
        var editable=false;
        var window=new SettingsWindow(new(),_=>Task.CompletedTask,_=>{},history:new(()=>new AppSettings(HistoryDirectory:Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")))),canEdit:()=>editable);
        try
        {
            window.Show();window.SelectHistory();
            var sections=window.GetLogicalDescendants().OfType<TabControl>().Single(c=>AutomationProperties.GetName(c)=="Settings sections");
            Assert.Equal(3,sections.ItemCount);Assert.Equal(2,sections.SelectedIndex);
            Assert.False(window.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Save settings").IsVisible);
            sections.SelectedIndex=0;Assert.Equal(2,sections.SelectedIndex);
            editable=true;window.RefreshEditAccess();sections.SelectedIndex=0;Assert.Equal(0,sections.SelectedIndex);
            Assert.True(window.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Save settings").IsVisible);
        }
        finally{window.Close();}
    });
    [Theory]
    [InlineData(AppLanguage.English,"History")]
    [InlineData(AppLanguage.Russian,"История")]
    public Task HistoryNavigationAndCaptionAreLocalized(AppLanguage language,string caption)=>ui.Run(()=>
    {
        var previous=Loc.Language;Loc.Language=language;
        var window=new SettingsWindow(new(),_=>Task.CompletedTask,_=>{},history:new(()=>new AppSettings(HistoryDirectory:Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")))));
        try
        {
            window.Show();window.SelectHistory();
            var navigation=window.GetLogicalDescendants().OfType<TabControl>().Single(c=>AutomationProperties.GetName(c)=="Settings sections");
            Assert.Equal(caption,((TabItem)navigation.SelectedItem!).Header);
        }
        finally{window.Close();Loc.Language=previous;}
    });
    [Fact]
    public Task RecycleFailureKeepsFileAndShowsError() => ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-error-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"2026-11"));var path=Path.Combine(root,"2026-11","app_20261104_101010.png");await File.WriteAllTextAsync(path,"fixture");
            using var page=new CaptureHistoryPage(new(()=>new AppSettings(HistoryDirectory:root)),recycle:(_,_)=>throw new IOException("Denied fixture"));
            await page.ActivateAsync();await page.RecycleFileAsync(path);
            Assert.True(File.Exists(path));Assert.Contains(page.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text=="Denied fixture");
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    });
}

