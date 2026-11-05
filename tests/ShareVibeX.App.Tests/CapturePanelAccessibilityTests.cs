using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Editing;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Localization;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class CapturePanelAccessibilityTests(UiTestFixture ui)
{
    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Russian)]
    public Task CaptureActionsKeepVectorIconsKeyboardFocusAndHintsInBothLanguages(AppLanguage language) => ui.Run(() =>
    {
        var previous = Loc.Language;
        Loc.Language = language;
        var editor = new EditorToolbar(new CaptureEditorState(), () => { }, _ => Task.CompletedTask, () => { });
        var mode = new CaptureModeToolbar(() => Task.CompletedTask, () => { }, () => { }, () => { }, _ => Task.CompletedTask);
        var recording = new RecordingControlWindow(() => Task.CompletedTask, () => Task.CompletedTask, () => { });
        try
        {
            foreach (var panel in new Control[] { editor, mode, recording })
            {
                var buttons = panel.GetLogicalDescendants().OfType<Button>().Where(button => button.Content is PathIcon).ToArray();
                Assert.NotEmpty(buttons);
                Assert.All(buttons, button =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
                    Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(button)?.ToString()));
                    Assert.True(button.Focusable);
                    var icon = Assert.IsType<PathIcon>(button.Content);
                    Assert.NotNull(icon.Data);
                    Assert.Equal(22, icon.Width);
                    Assert.Equal(22, icon.Height);
                });
            }
        }
        finally { recording.Dismiss(); Loc.Language = previous; }
    });
}
