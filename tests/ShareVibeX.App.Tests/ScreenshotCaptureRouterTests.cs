using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.App.Tests;

public sealed class ScreenshotCaptureRouterTests
{
    private static readonly DesktopSnapshot Snapshot = new(new(0, 0, 1, 1), [new("screen", new(0, 0, 1, 1), 1)], new(1, 1, 4, new byte[4]));

    [Theory]
    [InlineData(CaptureBackend.Automatic, 1, 0, CaptureBackend.WindowsGraphicsCapture)]
    [InlineData(CaptureBackend.WindowsGraphicsCapture, 1, 0, CaptureBackend.WindowsGraphicsCapture)]
    [InlineData(CaptureBackend.Gdi, 0, 1, CaptureBackend.Gdi)]
    public async Task RoutesAndReportsSuccessfulBackend(CaptureBackend preference, int wgcCalls, int gdiCalls, CaptureBackend effective)
    {
        var wgc = new Source(); var gdi = new Source { Monitors = [new("gdi", new(0, 0, 1, 1), 1)] }; ScreenshotCaptureReport? report = null;
        var router = new ScreenshotCaptureRouter(wgc, gdi, () => preference, value => report = value);
        Assert.Same(gdi.GetMonitors(), router.GetMonitors());
        Assert.Same(Snapshot, await router.CaptureAsync());
        Assert.Equal(wgcCalls, wgc.Calls); Assert.Equal(gdiCalls, gdi.Calls);
        Assert.Equal(new ScreenshotCaptureReport(preference, effective, null), report);
    }

    [Fact]
    public async Task AutomaticFallsBackOnlyForTypedFailureAndSnapshotsPreference()
    {
        var reads = 0; var wgc = new Source { Error = new CaptureSourceUnavailableException("unsupported") }; var gdi = new Source();
        ScreenshotCaptureReport? report = null;
        var router = new ScreenshotCaptureRouter(wgc, gdi, () => { reads++; return reads == 1 ? CaptureBackend.Automatic : CaptureBackend.WindowsGraphicsCapture; }, value => report = value);
        await router.CaptureAsync();
        Assert.Equal(1, reads); Assert.Equal(1, gdi.Calls);
        Assert.Equal(new ScreenshotCaptureReport(CaptureBackend.Automatic, CaptureBackend.Gdi, "unsupported"), report);
    }

    [Fact]
    public async Task StrictWgcPropagatesTypedFailure()
    {
        var error = new CaptureSourceUnavailableException("unsupported", new IOException());
        var wgc = new Source { Error = error }; var gdi = new Source();
        Assert.Same(error, await Record.ExceptionAsync(() => new ScreenshotCaptureRouter(wgc, gdi, () => CaptureBackend.WindowsGraphicsCapture).CaptureAsync()));
        Assert.Equal(0, gdi.Calls);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task OtherFailuresNeverFallbackOrReport(int kind)
    {
        Exception error = kind switch { 0 => new OperationCanceledException(), 1 => new InvalidOperationException("topology changed"), 2 => new InvalidDataException(), 3 => new IOException(), _ => new OutOfMemoryException() };
        var wgc = new Source { Error = error }; var gdi = new Source(); var reports = 0;
        Assert.Same(error, await Record.ExceptionAsync(() => new ScreenshotCaptureRouter(wgc, gdi, () => CaptureBackend.Automatic, _ => reports++).CaptureAsync()));
        Assert.Equal(0, gdi.Calls); Assert.Equal(0, reports);
    }

    [Fact]
    public async Task CancellationDuringTypedFailurePreventsFallback()
    {
        using var cancellation = new CancellationTokenSource(); var gdi = new Source();
        var wgc = new Source { Before = cancellation.Cancel, Error = new CaptureSourceUnavailableException("timeout") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ScreenshotCaptureRouter(wgc, gdi, () => CaptureBackend.Automatic).CaptureAsync(cancellation.Token));
        Assert.Equal(0, gdi.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObserverFailureDoesNotRecaptureOrFailSuccess(bool fallback)
    {
        var wgc = new Source { Error = fallback ? new CaptureSourceUnavailableException("unavailable") : null }; var gdi = new Source();
        Assert.Same(Snapshot, await new ScreenshotCaptureRouter(wgc, gdi, () => CaptureBackend.Automatic, _ => throw new CaptureSourceUnavailableException("observer")).CaptureAsync());
        Assert.Equal(1, wgc.Calls); Assert.Equal(fallback ? 1 : 0, gdi.Calls);
    }

    [Fact]
    public async Task AlreadyCanceledRequestDoesNotAcquireOrReport()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var wgc = new Source(); var gdi = new Source(); var reports = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ScreenshotCaptureRouter(wgc, gdi, () => CaptureBackend.Automatic, _ => reports++).CaptureAsync(cancellation.Token));
        Assert.Equal(0, wgc.Calls); Assert.Equal(0, gdi.Calls); Assert.Equal(0, reports);
    }

    [Fact]
    public async Task InvalidPreferenceDoesNotAcquireOrReport()
    {
        var wgc = new Source(); var gdi = new Source(); var reports = 0;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new ScreenshotCaptureRouter(wgc, gdi, () => (CaptureBackend)99, _ => reports++).CaptureAsync());
        Assert.Equal(0, wgc.Calls); Assert.Equal(0, gdi.Calls); Assert.Equal(0, reports);
    }

    [Theory]
    [InlineData(CaptureBackend.Automatic)]
    [InlineData(CaptureBackend.WindowsGraphicsCapture)]
    [InlineData(CaptureBackend.Gdi)]
    public async Task LateCancellationDoesNotReportOrFallback(CaptureBackend backend)
    {
        using var cancellation = new CancellationTokenSource();
        var wgc = new Source { Before = cancellation.Cancel }; var gdi = new Source { Before = cancellation.Cancel }; var reports = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ScreenshotCaptureRouter(wgc, gdi, () => backend, _ => reports++).CaptureAsync(cancellation.Token));
        Assert.Equal(backend == CaptureBackend.Gdi ? 0 : 1, wgc.Calls);
        Assert.Equal(backend == CaptureBackend.Gdi ? 1 : 0, gdi.Calls);
        Assert.Equal(0, reports);
    }

    [Fact]
    public async Task FailedFallbackDoesNotReport()
    {
        var gdi = new Source { Error = new IOException("gdi") }; var reports = 0;
        await Assert.ThrowsAsync<IOException>(() => new ScreenshotCaptureRouter(new Source { Error = new CaptureSourceUnavailableException("wgc") }, gdi, () => CaptureBackend.Automatic, _ => reports++).CaptureAsync());
        Assert.Equal(0, reports);
    }

    private sealed class Source : IDesktopCapture
    {
        public int Calls;
        public Exception? Error;
        public Action? Before;
        public IReadOnlyList<MonitorInfo> Monitors = Snapshot.Monitors;
        public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors;
        public Task<DesktopSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
        {
            Calls++; Before?.Invoke();
            return Error is {} error ? Task.FromException<DesktopSnapshot>(error) : Task.FromResult(Snapshot);
        }
    }
}
