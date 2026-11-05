using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Editing;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class MinimalCaptureUiTests(UiTestFixture ui)
{
    [Fact]
    public Task ChosenRegionOffersThreeAccessibleVectorIcons() => ui.Run(() =>
    {
        var toolbar = new CaptureModeToolbar(() => Task.CompletedTask, () => { }, () => { }, () => { });
        toolbar.Update(new CaptureEditorState { Region = new(0, 0, 140, 80) }, CaptureInteractionMode.Choosing);
        var choosing = (Panel)((StackPanel)toolbar.Child!).Children[0];
        var buttons = choosing.Children.OfType<Button>().ToArray();
        Assert.Equal(3, buttons.Length);
        Assert.Equal(new[] { "Edit screenshot", "Video", "Cancel capture" }, buttons.Select(AutomationProperties.GetName));
        Assert.All(buttons, button => { Assert.IsType<PathIcon>(button.Content); Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(button)?.ToString())); });
    });
    [Fact]
    public Task GeneralDoesNotExposePortableDataLocation() => ui.Run(() =>
    {
        var window = new SettingsWindow(new AppSettings(), _ => Task.CompletedTask, _ => { });
        var labels = window.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();
        Assert.DoesNotContain("Portable data", labels); Assert.DoesNotContain("Settings folder", labels);
    });
}
