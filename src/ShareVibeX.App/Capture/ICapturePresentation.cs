using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Capture;

public interface ICapturePresentation
{
    Task HideAndWaitAsync(CancellationToken token);
    Task<CaptureSession?> SelectAsync(DesktopLayout layout, CancellationToken token);
    Task ShowResultAsync(CaptureSession session, CancellationToken token);
    void Restore();
    void ShowError(string message);
}
