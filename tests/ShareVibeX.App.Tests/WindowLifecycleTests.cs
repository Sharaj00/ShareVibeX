using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using ShareVibeX.App.Capture;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class WindowLifecycleTests(UiTestFixture ui)
{
    [Fact]
    public Task ClosedHiddenWindowIsNotReopenedAfterCapture()=>ui.Run(()=>
    {
        var window=new Window();window.Show();var visibility=new WindowVisibilitySnapshot();
        visibility.Hide([window]);window.Close();
        var error=Record.Exception(visibility.Restore);
        Assert.Null(error);Assert.False(window.IsVisible);
        visibility.Restore();
    });
    [Fact]
    public Task ClosedOwnerAndChildAreNotRestored()=>ui.Run(()=>
    {
        var owner=new Window();var child=new Window();owner.Show();child.Show(owner);
        var visibility=new WindowVisibilitySnapshot();visibility.Hide([owner,child]);owner.Close();child.Close();
        Assert.Null(Record.Exception(visibility.Restore));Assert.False(owner.IsVisible);Assert.False(child.IsVisible);
    });
    [Fact]
    public async Task OwnedSettingsAreRestoredWithTheirOwnerAfterCapture()
    {
        await ui.Run(() =>
        {
            var main = new Window(); var settings = new Window();
            main.Show(); settings.Show(main);
            var visibility = new WindowVisibilitySnapshot();
            try
            {
                visibility.Hide([main,settings]);
                Assert.False(main.IsVisible); Assert.False(settings.IsVisible);
                visibility.Restore();
                Assert.True(main.IsVisible); Assert.True(settings.IsVisible); Assert.Same(main,settings.Owner);
            }
            finally { settings.Close(); main.Close(); }
        });
    }

}

public static class HeadlessEntry
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false });
}
