using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Localization;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class CaptureBackendUiTests(UiTestFixture ui)
{
    [Theory]
    [InlineData(AppLanguage.English)] [InlineData(AppLanguage.Russian)]
    public Task CaptureMethodSelectionSavesWithoutChangingOtherCaptureOptions(AppLanguage language)=>ui.Run(async()=>
    {
        var previous=Loc.Language;Loc.Language=language;
        var saved=new TaskCompletionSource<AppSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        var original=new AppSettings(Language:language,CaptureHotkey:"Alt+F8",Selection:new(){ScreenCrosshair=true,EditorDimPercent=25},CaptureBackend:CaptureBackend.Gdi);
        var window=new SettingsWindow(original,value=>{saved.TrySetResult(value);return Task.CompletedTask;},_=>{});
        try
        {
            window.Show();
            var sections=window.GetLogicalDescendants().OfType<TabControl>().Single(c=>AutomationProperties.GetName(c)=="Settings sections");sections.SelectedIndex=1;
            var captureTabs=Controls(window).OfType<TabControl>().Single(t=>AutomationProperties.GetName(t)=="Capture settings tabs");
            IEnumerable<Control> ActiveTab()=>Controls((Control)((TabItem)captureTabs.SelectedItem!).Content!);
            var combo=ActiveTab().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Screenshot capture method");
            Assert.Equal(CaptureBackend.Gdi,combo.SelectedItem);
            Assert.Equal(3,combo.ItemCount);combo.SelectedItem=CaptureBackend.WindowsGraphicsCapture;
            captureTabs.SelectedIndex=2;
            Assert.DoesNotContain(ActiveTab().OfType<ComboBox>(),c=>AutomationProperties.GetName(c)=="Screenshot capture method");
            captureTabs.SelectedIndex=1;
            Assert.Equal(Loc.T("Applies to screenshots and structured video recording. Custom FFmpeg commands keep their explicit source."),ToolTip.GetTip((Control)combo.Parent!));
            window.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Save settings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var result=await saved.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(CaptureBackend.WindowsGraphicsCapture,result.CaptureBackend);Assert.Equal(original.Selection,result.Selection);
            Assert.Equal(original.RecordingDefaults,result.RecordingDefaults);Assert.Equal(original.CaptureHotkey,result.CaptureHotkey);
        }
        finally{window.Close();Loc.Language=previous;}
    });
    [Fact] public Task EncoderMenusUseApplicableTunesAndSoftwarePresets()=>ui.Run(()=>
    {
        var page=new VideoSettingsPage(new());var window=new Window{Content=page};
        try
        {
            window.Show();
            ComboBox Find(string name)=>Controls(window).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)==name);
            var encoder=Find("Video encoder");var preset=Find("Encoder preset");var tune=Find("Encoder tune");
            Assert.Contains("placebo",preset.Items.Cast<string>());Assert.True(tune.IsEnabled);
            preset.SelectedItem="placebo";tune.SelectedItem="film";
            encoder.SelectedItem=ShareVibeX.Core.Recording.VideoEncoder.H264Qsv;
            Assert.Equal("auto",preset.SelectedItem);Assert.Equal("",tune.SelectedItem);Assert.False(tune.IsEnabled);
            Assert.DoesNotContain("placebo",preset.Items.Cast<string>());
            encoder.SelectedItem=ShareVibeX.Core.Recording.VideoEncoder.Libx264;
            Assert.Equal("placebo",preset.SelectedItem);Assert.Equal("film",tune.SelectedItem);Assert.True(tune.IsEnabled);
            encoder.SelectedItem=ShareVibeX.Core.Recording.VideoEncoder.LibvpxVp9;
            var quality=Controls(window).OfType<NumericUpDown>().Single(c=>AutomationProperties.GetName(c)=="Video quality");
            Assert.Equal(63,quality.Maximum);quality.Value=63;
            encoder.SelectedItem=ShareVibeX.Core.Recording.VideoEncoder.Libx264;Assert.Equal(51,quality.Maximum);
            encoder.SelectedItem=ShareVibeX.Core.Recording.VideoEncoder.LibvpxVp9;Assert.Equal(63,quality.Value);
            var container=Controls(window).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Video container");
            Assert.DoesNotContain(ShareVibeX.Core.Recording.VideoContainer.Mp4,container.Items.Cast<ShareVibeX.Core.Recording.VideoContainer>());
            container.SelectedItem=ShareVibeX.Core.Recording.VideoContainer.Webm;
            Assert.Equal(ShareVibeX.Core.Recording.AudioEncoder.Libopus,page.GetOptions().AudioEncoder);
            encoder.SelectedItem=ShareVibeX.Core.Recording.VideoEncoder.Libx264;
            Assert.Equal(ShareVibeX.Core.Recording.VideoContainer.Mp4,container.SelectedItem);
        }
        finally{window.Close();page.Dispose();}
    });
    private static IEnumerable<Control> Controls(Control root)
    {
        var controls=root.GetLogicalDescendants().OfType<Control>().ToList();
        foreach(var tabs in controls.OfType<TabControl>().ToArray())
            foreach(var item in tabs.Items.Cast<TabItem>())
                if(item.Content is Control content)controls.AddRange(content.GetLogicalDescendants().OfType<Control>());
        return controls.Distinct();
    }
}
