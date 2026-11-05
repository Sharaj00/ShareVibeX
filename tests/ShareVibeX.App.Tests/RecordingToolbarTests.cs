using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using ShareVibeX.App.Capture;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class RecordingToolbarTests(UiTestFixture ui)
{
    [Fact]
    public Task VideoReadyUsesFourIconsAndIndependentSourceChoices()=>ui.Run(()=>
    {
        RecordingSourceChoices? received=null;
        var toolbar=new CaptureModeToolbar(()=>Task.CompletedTask,()=>{},()=>{},()=>{},sources=>{received=sources;return Task.CompletedTask;});
        var buttons=toolbar.GetLogicalDescendants().OfType<Button>().Where(button=>new[]{"Record","Sound","Microphone","Back"}.Contains(AutomationProperties.GetName(button))).ToArray();
        Assert.Equal(4,buttons.Length);
        Assert.All(buttons,button=>{Assert.IsType<PathIcon>(button.Content);Assert.NotNull(ToolTip.GetTip(button));});
        Assert.Equal(new(true,false),toolbar.Sources);
        var sound=Assert.IsType<ToggleButton>(buttons.Single(button=>AutomationProperties.GetName(button)=="Sound"));
        var mic=Assert.IsType<ToggleButton>(buttons.Single(button=>AutomationProperties.GetName(button)=="Microphone"));
        sound.IsChecked=false;mic.IsChecked=true;
        buttons.Single(button=>AutomationProperties.GetName(button)=="Record").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new(false,true),received);
        return Task.CompletedTask;
    });
}
