using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.App.Capture;

public sealed class OverlayFrameCapture(IDesktopCapture capture, Action hide, Action restore, Func<CancellationToken, Task> waitForDesktop)
{
    public async Task<DesktopSnapshot> TakeAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            hide(); await waitForDesktop(token); token.ThrowIfCancellationRequested();
            var snapshot = await capture.CaptureAsync(token);
            token.ThrowIfCancellationRequested(); return snapshot;
        }
        finally { restore(); }
    }
}
