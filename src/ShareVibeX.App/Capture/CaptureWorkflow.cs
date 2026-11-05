using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.App.Capture;

public sealed class CaptureWorkflow(CaptureCoordinator coordinator, IDesktopCapture capture, ICapturePresentation presentation)
{
    private CancellationTokenSource? active;
    private long generation;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (active is not null || !coordinator.TryBeginSelection()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        active = linked;
        var request = ++generation;
        var token = linked.Token;
        try
        {
            DiagnosticLog.Write("Capture: hide");
            await presentation.HideAndWaitAsync(token);
            var layout = DesktopLayout.FromMonitors(capture.GetMonitors());
            token.ThrowIfCancellationRequested();
            if (request != generation) return;
            CheckTopology(layout);
            DiagnosticLog.Write("Capture: selection");
            var result = await presentation.SelectAsync(layout, token);
            token.ThrowIfCancellationRequested();
            if (request != generation || result is null) return;
            CheckTopology(layout);
            if (result.Snapshot.Bounds != layout.Bounds || !result.Snapshot.Monitors.SequenceEqual(layout.Monitors))
                throw new InvalidOperationException(Loc.T("The display layout changed. Start a new capture."));
            coordinator.SetSnapshot(result.Snapshot);
            coordinator.CompleteSelection(result.Region);
            await presentation.ShowResultAsync(coordinator.Session!, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { DiagnosticLog.Write("Capture failed: " + error); presentation.ShowError(error.Message); }
        finally
        {
            coordinator.EndSession();
            active = null;
            try{presentation.Restore();}
            catch(Exception error)
            {
                DiagnosticLog.Write("Capture cleanup failed: "+error);
                presentation.ShowError(Loc.T("Capture finished, but restoring an application window failed: ")+error.Message);
            }
        }
    }

    private void CheckTopology(DesktopLayout layout)
    {
        if (!capture.GetMonitors().SequenceEqual(layout.Monitors))
            throw new InvalidOperationException(Loc.T("The display layout changed. Start a new capture."));
    }

    public void Cancel()
    {
        generation++;
        active?.Cancel();
    }
}
