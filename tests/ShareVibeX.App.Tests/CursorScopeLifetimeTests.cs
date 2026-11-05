using Avalonia.Controls;
using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class CursorScopeLifetimeTests(UiTestFixture ui)
{
    [Fact] public Task SetupFailureReleasesCursorRestriction()=>ui.Run(async()=>
    {
        using var clipboard=new ClipboardImageOwner();var released=0;
        var controller=new SelectionOverlayController(new Source(),clipboard,new AppSettings(Selection:new(){RestrictToActiveMonitor=true}),windowFactory:()=>throw new IOException("Window failure"),cursorPosition:()=>new(-100,100),constrainCursor:bounds=>{Assert.Equal(new PixelRect(-800,0,800,600),bounds);return new Release(()=>released++);});
        await Assert.ThrowsAsync<IOException>(()=>controller.SelectAsync(DesktopLayout.FromMonitors(new Source().GetMonitors()),default));Assert.Equal(1,released);
    });
    [Fact] public Task CancellationReleasesCursorRestrictionAndClosesOverlay()=>ui.Run(async()=>
    {
        using var clipboard=new ClipboardImageOwner();using var cancellation=new CancellationTokenSource();var released=0;var window=new Window();
        var controller=new SelectionOverlayController(new Source(),clipboard,new AppSettings(Selection:new(){RestrictToActiveMonitor=true}),windowFactory:()=>window,cursorPosition:()=>new(-100,100),constrainCursor:_=>new Release(()=>released++));
        var task=controller.SelectAsync(DesktopLayout.FromMonitors(new Source().GetMonitors()),cancellation.Token);cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);Assert.Equal(1,released);Assert.False(window.IsVisible);
    });
    private sealed class Release(Action action):IDisposable{public void Dispose()=>action();}
    private sealed class Source:IDesktopCapture
    {
        public IReadOnlyList<MonitorInfo> GetMonitors()=>[new("left",new(-800,0,800,600),1)];
        public Task<DesktopSnapshot> CaptureAsync(CancellationToken token=default)=>throw new Exception("No capture expected");
    }
}
