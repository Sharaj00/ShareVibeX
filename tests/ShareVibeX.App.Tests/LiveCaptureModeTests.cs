using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.App.Tests;

public sealed class LiveCaptureModeTests
{
    private static readonly DesktopLayout Layout = DesktopLayout.FromMonitors([new("screen", new(-2, 0, 2, 1), 1)]);
    private static DesktopSnapshot Snapshot(byte value) => new(Layout.Bounds, Layout.Monitors, new(2, 1, 8, [value, 0, 0, 255, value, 0, 0, 255]));

    [Fact]
    public void ChoosingAndVideoDoNotAcquireOrFreezePixels()
    {
        var captures = 0; var session = new CaptureModeSession(Layout, _ => { captures++; return Task.FromResult(Snapshot(1)); });
        Assert.Equal(CaptureInteractionMode.Choosing, session.Mode); Assert.Null(session.Frozen);
        session.SelectVideo(); Assert.Equal(CaptureInteractionMode.Video, session.Mode);
        session.BackToChoosing(); Assert.Equal(CaptureInteractionMode.Choosing, session.Mode);
        Assert.Equal(0, captures); Assert.Null(session.Frozen);
    }
    [Fact]
    public async Task QuickScreenshotUsesCurrentPixelsWithoutEnteringEditor()
    {
        byte value = 1; var session = new CaptureModeSession(Layout, _ => Task.FromResult(Snapshot(value)));
        value = 91; var first = await session.ScreenshotAsync(default);
        value = 173; var second = await session.ScreenshotAsync(default);
        Assert.Equal(91, first.Frame.Bgra32[0]); Assert.Equal(173, second.Frame.Bgra32[0]);
        Assert.Equal(CaptureInteractionMode.Choosing, session.Mode); Assert.Null(session.Frozen);
    }
    [Fact]
    public async Task ExplicitEditorEntryFreezesFreshFrameOnce()
    {
        byte value = 81; var captures = 0;
        var session = new CaptureModeSession(Layout, _ => { captures++; return Task.FromResult(Snapshot(value)); });
        await session.EnterEditorAsync(default); value = 200;
        var exported = await session.ScreenshotAsync(default);
        Assert.Same(session.Frozen, exported); Assert.Equal(81, exported.Frame.Bgra32[0]); Assert.Equal(1, captures);
        Assert.Equal(CaptureInteractionMode.EditingScreenshot, session.Mode);
    }
    [Fact]
    public async Task LateCancelledFrameCannotEnterEditor()
    {
        var pending = new TaskCompletionSource<DesktopSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new CaptureModeSession(Layout, _ => pending.Task);
        using var cancellation = new CancellationTokenSource();
        var entering = session.EnterEditorAsync(cancellation.Token); cancellation.Cancel(); pending.SetResult(Snapshot(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entering);
        Assert.Null(session.Frozen); Assert.Equal(CaptureInteractionMode.Choosing, session.Mode);
    }
    [Fact]
    public async Task ChangedTopologyCannotBeFrozen()
    {
        var changed = Snapshot(1) with { Monitors = [new("other", new(0, 0, 2, 1), 1)] };
        var session = new CaptureModeSession(Layout, _ => Task.FromResult(changed));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.EnterEditorAsync(default)); Assert.Null(session.Frozen);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FreshCaptureHidesOverlaysAndRestoresOnSuccessOrFailure(bool fail)
    {
        var events = new List<string>();
        var source = new Source(() => { events.Add("capture"); if (fail) throw new IOException("capture failed"); return Snapshot(1); });
        var service = new OverlayFrameCapture(source, () => events.Add("hide"), () => events.Add("restore"), _ => { events.Add("compositor"); return Task.CompletedTask; });
        if (fail) await Assert.ThrowsAsync<IOException>(() => service.TakeAsync(default)); else await service.TakeAsync(default);
        Assert.Equal(new[] { "hide", "compositor", "capture", "restore" }, events);
    }
    private sealed class Source(Func<DesktopSnapshot> capture) : IDesktopCapture
    {
        public IReadOnlyList<MonitorInfo> GetMonitors() => Layout.Monitors;
        public Task<DesktopSnapshot> CaptureAsync(CancellationToken token = default) => Task.FromResult(capture());
    }
}
