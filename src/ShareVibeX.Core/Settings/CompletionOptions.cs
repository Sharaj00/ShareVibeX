namespace ShareVibeX.Core.Settings;

public enum CompletionPlacement { BottomRight, BottomLeft, TopRight, TopLeft }

public enum CompletionClickAction { None, OpenFile, OpenFolder, Close }

public sealed record CompletionOptions
{
    public string? CustomSoundPath {get;init;}
    public CompletionClickAction LeftAction {get;init;}=CompletionClickAction.None;
    public CompletionClickAction RightAction {get;init;}=CompletionClickAction.Close;
    public CompletionClickAction MiddleAction {get;init;}=CompletionClickAction.None;
    public bool ShowNotifications {get;init;}
    public bool PlaySound {get;init;}
    public bool SuppressFullscreen {get;init;}=true;
    public int DurationSeconds {get;init;}=3;
    public double FadeSeconds {get;init;}=1;
    public int Width {get;init;}=360;
    public int Height {get;init;}=140;
    public CompletionPlacement Placement {get;init;}=CompletionPlacement.BottomRight;
}
