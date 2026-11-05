using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using ShareVibeX.App.Views;
using ShareVibeX.App.Capture;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Localization;
namespace ShareVibeX.App.Tests;
[Collection("Avalonia UI")]
public sealed class AnimationSettingsTests(UiTestFixture ui)
{
    [Theory][InlineData(false)][InlineData(true)]
    public Task OpenEncoderListCommitsTheLastAnimationItems(bool webp)=>ui.Run(()=>
    {
        var theme=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(theme);
        using var page=new VideoSettingsPage(new());var window=new Window{Width=900,Height=1000,Content=page};
        try
        {
            window.Show();window.UpdateLayout();
            var combo=Controls(page).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Video encoder");
            Assert.True(combo.MaxDropDownHeight < window.Height/2);
            combo.Focus();combo.IsDropDownOpen=true;window.UpdateLayout();
            var target=webp?VideoEncoder.LibwebpAnim:VideoEncoder.Apng;
            var index=combo.Items.Cast<VideoEncoder>().ToList().IndexOf(target);combo.ScrollIntoView(index);window.UpdateLayout();
            var item=combo.ContainerFromIndex(index)!;Assert.NotNull(item);
            var popup=TopLevel.GetTopLevel(item)!;popup.UpdateLayout();Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var point=item.TranslatePoint(new Avalonia.Point(item.Bounds.Width/2,item.Bounds.Height/2),popup)!.Value;
            Avalonia.Headless.HeadlessWindowExtensions.MouseDown(popup,point,Avalonia.Input.MouseButton.Left);
            Avalonia.Headless.HeadlessWindowExtensions.MouseUp(popup,point,Avalonia.Input.MouseButton.Left);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(webp?VideoEncoder.LibwebpAnim:VideoEncoder.Apng,page.GetOptions().Encoder);
        }
        finally{window.Close();Avalonia.Application.Current!.Styles.Remove(theme);}
    });
    [Theory]
    [InlineData(VideoEncoder.Gif,VideoEncoder.LibwebpAnim)]
    [InlineData(VideoEncoder.Gif,VideoEncoder.Apng)]
    [InlineData(VideoEncoder.LibwebpAnim,VideoEncoder.Apng)]
    [InlineData(VideoEncoder.Apng,VideoEncoder.LibwebpAnim)]
    public Task LoadedAnimationCanSwitchToAnotherAnimation(VideoEncoder initial,VideoEncoder target)=>ui.Run(()=>
    {
        using var page=new VideoSettingsPage(new RecordingOptions().SwitchEncoder(initial));
        var combo=Controls(page).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Video encoder");
        Assert.True(combo.IsEnabled);combo.SelectedItem=target;
        Assert.Equal(target,page.GetOptions().Encoder);
    });
    [Theory]
    [InlineData(VideoEncoder.Gif,AppLanguage.English)][InlineData(VideoEncoder.LibwebpAnim,AppLanguage.English)][InlineData(VideoEncoder.Apng,AppLanguage.English)]
    [InlineData(VideoEncoder.Gif,AppLanguage.Russian)][InlineData(VideoEncoder.LibwebpAnim,AppLanguage.Russian)][InlineData(VideoEncoder.Apng,AppLanguage.Russian)]
    public Task SelectingAnimationProducesValidSilentSettings(VideoEncoder encoder,AppLanguage language)=>ui.Run(()=>
    {
        var old=Loc.Language;Loc.Language=language;using var page=new VideoSettingsPage(new(){RateControl=VideoRateControl.Bitrate});
        try
        {
            var controls=Controls(page);T Find<T>(string name)where T:Control=>controls.OfType<T>().Single(c=>AutomationProperties.GetName(c)==name);
            Find<ComboBox>("Video encoder").SelectedItem=encoder;
            Assert.Single(Find<ComboBox>("Video container").Items);Assert.False(((Control)Find<ComboBox>("Video container").Parent!).IsVisible);Assert.False(Find<ComboBox>("Rate control").IsEnabled);
            Assert.False(Find<ComboBox>("Command preview audio mode").IsEnabled);Assert.False(Find<CheckBox>("Use custom command").IsEnabled);
            Find<NumericUpDown>("Animation loop count").Value=3;
            var options=page.GetOptions();Assert.True(options.IsAnimation);Assert.Equal(3,options.AnimationLoopCount);Assert.Equal(VideoRateControl.Quality,options.RateControl);
            Assert.False(RecordingSourceChoices.FromOptions(options).WithAudio);
            if(encoder==VideoEncoder.Gif)
            {
                Find<NumericUpDown>("GIF colors").Value=16;
                Find<ComboBox>("GIF dithering").SelectedItem=GifDither.Bayer;
                var gif=page.GetOptions();
                Assert.Equal(16,gif.GifColors);Assert.Equal(GifDither.Bayer,gif.GifDither);
            }
            if(encoder==VideoEncoder.LibwebpAnim)
            {
                Find<CheckBox>("Lossless WebP").IsChecked=true;Find<NumericUpDown>("Video quality").Value=90;
                Find<ComboBox>("Video encoder").SelectedItem=VideoEncoder.Libx264;
                Find<ComboBox>("Video encoder").SelectedItem=encoder;
                Assert.True(page.GetOptions().AnimationLossless);Assert.Equal(90,page.GetOptions().Quality);Assert.Equal(3,page.GetOptions().AnimationLoopCount);
            }
        }
        finally{Loc.Language=old;}
    });
    [Theory][InlineData(AppLanguage.English)][InlineData(AppLanguage.Russian)]
    public Task AnimationReadyToolbarCannotEnableSoundOrMicrophone(AppLanguage language)=>ui.Run(()=>
    {
        var old=Loc.Language;Loc.Language=language;
        try
        {
            var bar=new CaptureModeToolbar(()=>Task.CompletedTask,()=>{},()=>{},()=>{},sources:new(true,true),animation:true);
            bar.SetMicrophoneAvailable(true);bar.SetSources(new(true,true));Assert.False(bar.Sources.WithAudio);
            foreach(var button in bar.GetLogicalDescendants().OfType<ToggleButton>())
            {Assert.False(button.IsEnabled);Assert.Equal(Loc.T("Animations are silent."),ToolTip.GetTip(button));}
        }
        finally{Loc.Language=old;}
    });
    private static List<Control> Controls(Control root)
    {
        var controls=root.GetLogicalDescendants().OfType<Control>().ToList();
        foreach(var tabs in controls.OfType<TabControl>().ToArray())foreach(var tab in tabs.Items.Cast<TabItem>())
            if(tab.Content is Control child)controls.AddRange(child.GetLogicalDescendants().OfType<Control>());
        return controls.Distinct().ToList();
    }
}


