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
        Assert.Contains(buttons,button=>Avalonia.Automation.AutomationProperties.GetName(button)=="Maximize or restore settings");
    });
}
