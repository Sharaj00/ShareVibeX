namespace ShareVibeX.Core.Capture;

public sealed class CaptureCoordinator
{
    public CaptureState State { get; private set; }
    public CaptureSession? Session { get; private set; }
    private DesktopSnapshot? snapshot;

    public bool TryBeginSelection()
    {
        if (State != CaptureState.Idle) return false;
        State = CaptureState.Selecting;
        return true;
    }

    public void SetSnapshot(DesktopSnapshot value)
    {
        if (State != CaptureState.Selecting) throw new InvalidOperationException(Loc.T("No selection is active."));
        snapshot = value;
    }

    public void CompleteSelection(PixelRect region)
    {
        if (State != CaptureState.Selecting || snapshot is null) throw new InvalidOperationException(Loc.T("A desktop snapshot is required."));
        if (region.IsEmpty || region.Intersect(snapshot.Bounds) != region) throw new ArgumentOutOfRangeException(nameof(region));
        Session = new(snapshot, region);
        State = CaptureState.Ready;
    }

    public void Cancel() => EndSession();

    public void EndSession()
    {
        Session = null;
        snapshot = null;
        State = CaptureState.Idle;
    }
}
