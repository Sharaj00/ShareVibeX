using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Recording;
using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class VideoSettingsTests(UiTestFixture ui)
{
    [Fact]
    public Task TimingAndAdvancedControlsBelongOnlyToPreviewTab()=>ui.Run(()=>
    {
        using var page=new VideoSettingsPage(new());var tabs=page.GetLogicalDescendants().OfType<TabControl>().Single();
        var preview=(Control)tabs.Items.Cast<TabItem>().ElementAt(3).Content!;
        var timing=Find<CheckBox>(page,"Show video timings");var advanced=Find<Expander>(page,"Advanced FFmpeg options");
        Assert.Contains(timing,preview.GetLogicalDescendants());Assert.Contains(advanced,preview.GetLogicalDescendants());
        foreach(var tab in tabs.Items.Cast<TabItem>().Take(3))
        {Assert.DoesNotContain(timing,((Control)tab.Content!).GetLogicalDescendants());Assert.DoesNotContain(advanced,((Control)tab.Content!).GetLogicalDescendants());}
        timing.IsChecked=false;Assert.False(page.GetOptions().ShowVideoTimings);
    });
    [Theory]
    [InlineData(654,false)] [InlineData(654,true)] [InlineData(834,false)] [InlineData(834,true)]
    public Task BilingualEncodingKeepsNumericTextAndTabsReadable(int width,bool russian)=>ui.Run(()=>
    {
        var old=ShareVibeX.Core.Localization.Loc.Language;
        ShareVibeX.Core.Localization.Loc.Language=russian?ShareVibeX.Core.Localization.AppLanguage.Russian:ShareVibeX.Core.Localization.AppLanguage.English;
        var fluent=new FluentTheme();Application.Current!.Styles.Add(fluent);
        using var page=new VideoSettingsPage(new());var window=new Window{Width=width,Height=1000,Content=page};
        try
        {
            window.Show();window.UpdateLayout();var fps=Find<NumericUpDown>(page,"Frame rate");Assert.True(fps.Bounds.Width>=70);
            var tabs=page.GetLogicalDescendants().OfType<TabControl>().Single();
            foreach(var tab in tabs.Items.Cast<TabItem>())Assert.IsType<TextBlock>(tab.Header);
            foreach(var label in page.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.Classes.Contains("video-field-label")&&t.IsEffectivelyVisible))
            {Assert.Equal(Avalonia.Media.TextWrapping.NoWrap,label.TextWrapping);Assert.True(label.Bounds.Width>=100);}
        }
        finally{window.Close();Application.Current!.Styles.Remove(fluent);ShareVibeX.Core.Localization.Loc.Language=old;}
    });
    [Fact]
    public Task VideoTimeVisibilityIsAnEditablePersistedOption()=>ui.Run(()=>
    {
        using var page=new VideoSettingsPage(new(){ShowVideoTimings=false});
        var checkbox=Find<CheckBox>(page,"Show video timings");Assert.False(checkbox.IsChecked);
        checkbox.IsChecked=true;Assert.True(page.GetOptions().ShowVideoTimings);
    });
    [Fact]
    public Task AudioEndpointsAreQueriedOnlyOnExplicitRefreshAndSelectionPersistsInDraft() => ui.Run(() =>
    {
        var audio=new FakeAudioFactory();using var page=new VideoSettingsPage(new(),audio:audio);
        Assert.Equal(0,audio.Queries);Assert.Equal(0,audio.Opened);
        Find<Button>(page,"Refresh audio devices").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1,audio.Queries);Assert.Equal(0,audio.Opened);
        var combo=Find<ComboBox>(page,"Playback device");
        combo.SelectedItem=combo.ItemsSource!.Cast<object>().Single(item=>item.ToString()!.StartsWith("Test speakers",StringComparison.Ordinal));
        Assert.Equal("playback-test",page.GetOptions().PlaybackDeviceId);
    });
    [Theory]
    [InlineData(654)] [InlineData(800)]
    public Task EncodingFieldsFitInsideTheirTabWithApplicationButtonPadding(int width) => ui.Run(() =>
    {
        var fluent = new FluentTheme(); Application.Current!.Styles.Add(fluent);
        using var page = new VideoSettingsPage(new());
        var window = new Window { Width = width, Height = 800, Content = page, FontSize = 14 };
        window.Styles.Add(new Style(selector => selector.OfType<Button>()) { Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(16, 10)) } });
        try
        {
            window.Show(); window.UpdateLayout();
            var tabs = page.GetLogicalDescendants().OfType<TabControl>().Single();
            foreach (var name in new[] { "Video container", "Audio channels" })
            {
                var control = page.GetVisualDescendants().OfType<Control>().SingleOrDefault(c => AutomationProperties.GetName(c) == name);
                Assert.NotNull(control);
                var position = control.TranslatePoint(new Point(0, 0), tabs)!.Value;
                Assert.True(position.Y + control.Bounds.Height <= tabs.Bounds.Height + 1,
                    $"{name} ends at {position.Y + control.Bounds.Height}, tab ends at {tabs.Bounds.Height} (width {width}).");
            }
        }
        finally { window.Close(); Application.Current!.Styles.Remove(fluent); }
    });
    [Fact]
    public Task DefaultVideoPageUsesCompactColumnsAndSecondaryTabs() => ui.Run(() =>
    {
        var fluent = new FluentTheme(); Application.Current!.Styles.Add(fluent);
        using var page = new VideoSettingsPage(new());
        var window = new Window { Width = 800, Height = 700, Content = page };
        try
        {
            window.Show(); page.Measure(new Size(800, double.PositiveInfinity));
            Assert.InRange(page.DesiredSize.Height, 200, 650);
            var tabs = page.GetLogicalDescendants().OfType<TabControl>().Single();
            Assert.Equal(new[] { "Encoding", "Scaling", "Devices", "Preview" }, tabs.Items.Cast<TabItem>().Select(item => (item.Header as TextBlock)?.Text));
            Assert.False(Find<TextBox>(page, "Custom FFmpeg template").IsVisible);
        }
        finally { window.Close(); Application.Current!.Styles.Remove(fluent); }
    });
    [Fact]
    public Task VideoDraftChangesAndResetDoNotMutateLoadedOptions() => ui.Run(() =>
    {
        var original = new RecordingOptions { FrameRate = 24 };
        using var page = new VideoSettingsPage(original);
        Find<NumericUpDown>(page, "Frame rate").Value = 60;
        Find<ComboBox>(page, "Scaling mode").SelectedItem = ScaleMode.DivideBy;
        Assert.Equal(60, page.GetOptions().FrameRate); Assert.Equal(ScaleMode.DivideBy, page.GetOptions().ScaleMode);
        Assert.Equal(24, original.FrameRate);
        Find<Button>(page, "Reset recording options").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new RecordingOptions(), page.GetOptions());
    });

    [Fact]
    public Task ChangingFfmpegPathCancelsAndDiscardsOldProbeResult() => ui.Run(async () =>
    {
        var pending = new TaskCompletionSource<FfmpegProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        using var page = new VideoSettingsPage(new(), (_, token) => { observed = token; return pending.Task; });
        var test = page.TestAsync();
        Find<TextBox>(page, "FFmpeg executable").Text = "new-ffmpeg.exe";
        Assert.True(observed.IsCancellationRequested);
        pending.SetResult(new("old-ffmpeg.exe", new("ffmpeg version old", new HashSet<string>(), new HashSet<string>(), new HashSet<string>())));
        await test;
        Assert.DoesNotContain("version old", page.ProbeStatus);
        Assert.Contains("Not tested", page.ProbeStatus);
    });

    [Fact]
    public Task ClosingPageCancelsProbeAndIgnoresResult() => ui.Run(async () =>
    {
        var pending = new TaskCompletionSource<FfmpegProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var page = new VideoSettingsPage(new(), (_, token) => { observed = token; return pending.Task; });
        var test = page.TestAsync(); page.Dispose();
        Assert.True(observed.IsCancellationRequested);
        pending.SetResult(new("old.exe", new("ffmpeg version late", new HashSet<string>(), new HashSet<string>(), new HashSet<string>())));
        await test; Assert.DoesNotContain("version late", page.ProbeStatus);
    });

    private static T Find<T>(VideoSettingsPage page, string name) where T : Control
    {
        var descendants = page.GetLogicalDescendants().OfType<Control>().ToList();
        foreach (var tab in descendants.OfType<TabControl>().ToArray())
            foreach (var item in tab.Items.Cast<TabItem>())
                if (item.Content is Control content) descendants.AddRange(content.GetLogicalDescendants().OfType<Control>());
        return descendants.Distinct().OfType<T>().Single(control => AutomationProperties.GetName(control) == name);
    }
    private sealed class FakeAudioFactory:IRecordingAudioFactory
    {
        public int Queries,Opened;
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token){Opened++;throw new InvalidOperationException("UI must not start capture.");}
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)
        {Queries++;return Task.FromResult<IReadOnlyList<RecordingAudioDevice>>([new("playback-test","Test speakers",RecordingAudioDeviceKind.Playback,true),new("mic-test","Test microphone",RecordingAudioDeviceKind.Microphone,true)]);}
    }
}
