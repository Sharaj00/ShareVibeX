namespace ShareVibeX.Core.Settings;

public enum TrayAction { None, CaptureRegion, OpenSettings, StopRecording, OpenHistory }
public enum TrayGesture { Single, Double, Middle }

public sealed class TrayGestureRouter(int doubleClickMilliseconds)
{
    private long? deadline;private bool suppressUp;
    public void LeftUp(long now)
    {
        if(suppressUp){suppressUp=false;return;}
        deadline=now+doubleClickMilliseconds;
    }
    public TrayGesture DoubleClick(){deadline=null;suppressUp=true;return TrayGesture.Double;}
    public TrayGesture? Advance(long now)
    {
        if(deadline is not {} due||now<due)return null;
        deadline=null;return TrayGesture.Single;
    }
    public void Cancel(){deadline=null;suppressUp=false;}
}
