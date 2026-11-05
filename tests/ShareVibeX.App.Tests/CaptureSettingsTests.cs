using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class CaptureSettingsTests(UiTestFixture ui)
{
    [Fact] public Task DetectionIsOptInAndEditsDoNotMutateLoadedOptions()=>ui.Run(()=>
    {
        var original=new SelectionOptions();var page=new CaptureSettingsPage(original);
        var theme=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(theme);
        var window=new Window{Content=page};
        try
        {
            window.Show();window.UpdateLayout();
            var panel=(Control)page.GetLogicalDescendants().OfType<TabControl>().Single().Items.Cast<TabItem>().First().Content!;
            var windows=panel.GetLogicalDescendants().OfType<CheckBox>().Single(c=>AutomationProperties.GetName(c)=="Detect window regions");
            var controls=panel.GetLogicalDescendants().OfType<CheckBox>().Single(c=>AutomationProperties.GetName(c)=="Detect child controls");
            Assert.False(windows.IsChecked);Assert.False(controls.IsEnabled);windows.IsChecked=true;Assert.True(controls.IsEnabled);controls.IsChecked=true;
            Assert.True(page.GetOptions().DetectWindows);Assert.True(page.GetOptions().DetectControls);Assert.False(original.DetectWindows);
        }
        finally{window.Close();Avalonia.Application.Current!.Styles.Remove(theme);}
    });
    [Fact] public async Task AdvancedSelectionOptionsPersistInPortableSettings()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-selection-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new PortableSettingsStore(root);var options=new SelectionOptions{ShowPosition=true,ScreenCrosshair=true,RestrictToActiveMonitor=true,SnapSizes="320x240;800x600",InfoTemplate="{x},{y}{newline}{width}x{height}",EditorDimPercent=25};
            await store.SaveAsync(new AppSettings(Selection:options));Assert.Equal(options,(await store.LoadAsync()).SelectionDefaults);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
