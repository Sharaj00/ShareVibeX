using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Capture;

public enum CaptureInteractionMode { Choosing, EditingScreenshot, Video }

public sealed class CaptureModeSession(DesktopLayout layout, Func<CancellationToken, Task<DesktopSnapshot>> capture)
{
    public CaptureInteractionMode Mode { get; private set; }
    public DesktopSnapshot? Frozen { get; private set; }
    private bool acquiring;
    public void SelectVideo() { RequireChoosing(); Mode = CaptureInteractionMode.Video; }
    public void BackToChoosing()
    {
        if (acquiring || Mode == CaptureInteractionMode.EditingScreenshot) throw new InvalidOperationException(Loc.T("Finish the current screenshot session first."));
        Mode = CaptureInteractionMode.Choosing;
    }
    public async Task EnterEditorAsync(CancellationToken token)
    {
        RequireChoosing();
        var frame = await FreshAsync(token);
        Frozen = frame; Mode = CaptureInteractionMode.EditingScreenshot;
    }
    public Task<DesktopSnapshot> ScreenshotAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Mode == CaptureInteractionMode.EditingScreenshot) return Task.FromResult(Frozen!);
        RequireChoosing(); return FreshAsync(token);
    }
    private void RequireChoosing()
    {
        if (acquiring || Mode != CaptureInteractionMode.Choosing) throw new InvalidOperationException(Loc.T("Choose screenshot editing or return to mode selection first."));
    }
    private async Task<DesktopSnapshot> FreshAsync(CancellationToken token)
    {
        acquiring = true;
        try
        {
            token.ThrowIfCancellationRequested();
            var snapshot = await capture(token);
            token.ThrowIfCancellationRequested();
            if (snapshot.Bounds != layout.Bounds || !snapshot.Monitors.SequenceEqual(layout.Monitors))
                throw new InvalidOperationException(Loc.T("The display layout changed. Start a new capture."));
            if (snapshot.Frame.Width != layout.Bounds.Width || snapshot.Frame.Height != layout.Bounds.Height ||
                snapshot.Frame.Stride < checked(snapshot.Frame.Width * 4) || snapshot.Frame.Bgra32.Length < checked(snapshot.Frame.Stride * snapshot.Frame.Height))
                throw new InvalidOperationException(Loc.T("The captured frame does not match the desktop layout."));
            return snapshot;
        }
        finally { acquiring = false; }
    }
}
