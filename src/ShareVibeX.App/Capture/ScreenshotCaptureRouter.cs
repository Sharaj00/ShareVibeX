using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.App.Capture;

public sealed record ScreenshotCaptureReport(CaptureBackend RequestedBackend, CaptureBackend EffectiveBackend, string? FallbackReason);

public sealed class ScreenshotCaptureRouter(IDesktopCapture wgc, IDesktopCapture gdi,
    Func<CaptureBackend> preference, Action<ScreenshotCaptureReport>? report = null) : IDesktopCapture
{
    public IReadOnlyList<MonitorInfo> GetMonitors() => gdi.GetMonitors();

    public async Task<DesktopSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = preference();
        if (!Enum.IsDefined(requested)) throw new ArgumentOutOfRangeException(nameof(preference), requested, "Unknown capture backend.");
        var effective = requested == CaptureBackend.Gdi ? CaptureBackend.Gdi : CaptureBackend.WindowsGraphicsCapture;
        string? fallbackReason = null;
        DesktopSnapshot snapshot;
        if (requested == CaptureBackend.Gdi)
            snapshot = await gdi.CaptureAsync(cancellationToken);
        else
        {
            try { snapshot = await wgc.CaptureAsync(cancellationToken); }
            catch (CaptureSourceUnavailableException error) when (requested == CaptureBackend.Automatic)
            {
                cancellationToken.ThrowIfCancellationRequested();
                snapshot = await gdi.CaptureAsync(cancellationToken);
                effective = CaptureBackend.Gdi;
                fallbackReason = error.Message;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        try { report?.Invoke(new(requested, effective, fallbackReason)); }
        catch (Exception error) { DiagnosticLog.Write("Screenshot report observer failed: " + error); }
        return snapshot;
    }
}
