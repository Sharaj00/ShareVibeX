using Avalonia;
using Avalonia.Automation;
using Avalonia.VisualTree;
using Avalonia.Styling;
using ShareVibeX.Core.Localization;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class SettingsChromeTests(UiTestFixture ui)
{
    [Fact]
    public Task SettingsUsesClientCaptionButtonsInsteadOfNativeHeader()=>ui.Run(()=>
    {
        var window=new SettingsWindow(new AppSettings(),_=>Task.CompletedTask,_=>{});
        Assert.Equal(WindowDecorations.None,window.WindowDecorations);
        var buttons=window.GetLogicalDescendants().OfType<Button>().ToArray();
        Assert.Contains(buttons,button=>Avalonia.Automation.AutomationProperties.GetName(button)=="Close settings");
        Assert.DoesNotContain(buttons,button=>Avalonia.Automation.AutomationProperties.GetName(button)=="Maximize or restore settings");
        Assert.False(window.CanResize);
        Assert.Equal(840d/720,window.Width/window.Height,8);
        Assert.True(Assert.IsType<Border>(window.Content).CornerRadius.TopLeft>0);
    });

    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Russian)]
    public Task ExpandedGeneralAndEncodingFitWithoutPageScrolling(AppLanguage language)=>ui.Run(()=>
    {
        var previous=Loc.Language;Loc.Language=language;
        var theme=new Avalonia.Themes.Fluent.FluentTheme();
        Application.Current!.Styles.Add(theme);
        var window=new SettingsWindow(AppSettings.FactoryDefaults with{Completion=new(){ShowNotifications=true}},_=>Task.CompletedTask,_=>{});
        window.Styles.Add(new Style(selector=>selector.OfType<Button>())
        {
            Setters={new Setter(Avalonia.Controls.Primitives.TemplatedControl.PaddingProperty,new Thickness(16,10))}
        });
        try
        {
            window.Show();window.UpdateLayout();
            var navigation=window.GetVisualDescendants().OfType<TabControl>()
                .Single(c=>AutomationProperties.GetName(c)=="Settings sections");
            var entries=navigation.Items.Cast<TabItem>().ToArray();
            var general=Assert.IsType<Grid>(Assert.IsType<Border>(entries[0].Content).Child);
            var rows=general.GetVisualDescendants().OfType<Border>().Where(row=>row.Classes.Contains("card")).ToArray();
            Assert.NotEmpty(rows);
            Assert.All(rows,row=>Assert.Equal(42,row.Bounds.Height));
            Assert.IsType<ScrollViewer>(Assert.IsType<Border>(entries[2].Content).Child);
            void CheckFits(Control target)
            {
                var page=Assert.IsType<Border>(((TabItem)navigation.SelectedItem!).Content);
                var bottom=target.TranslatePoint(new Point(target.Bounds.Width,target.Bounds.Height),page);
                Assert.NotNull(bottom);
                Assert.InRange(bottom.Value.X,1,page.Bounds.Width+1);
                Assert.InRange(bottom.Value.Y,1,page.Bounds.Height+1);
            }
            var checks=window.GetVisualDescendants().OfType<CheckBox>().ToArray();
            Assert.Null(checks.Single(c=>AutomationProperties.GetName(c)=="Play completion sound").Content);
            CheckFits(window.GetVisualDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Notification middle action"));
            CheckFits(window.GetVisualDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Tray middle click"));
            navigation.SelectedIndex=1;window.UpdateLayout();
            var capture=Assert.IsType<TabControl>(Assert.IsType<Border>(entries[1].Content).Child);
            Assert.Equal(new[]{Loc.T("Media"),Loc.T("Selection")},capture.Items.Cast<TabItem>().Select(tab=>tab.Header));
            CheckFits(window.GetVisualDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Image format"));
            CheckFits(window.GetVisualDescendants().OfType<NumericUpDown>().Single(c=>AutomationProperties.GetName(c)=="Recording countdown"));
            CheckFits(window.GetVisualDescendants().OfType<Slider>().Single(c=>AutomationProperties.GetName(c)=="Image quality"));
            CheckFits(window.GetVisualDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Video container"));
        }
        finally{window.Close();Application.Current!.Styles.Remove(theme);Loc.Language=previous;}
    });
}
