namespace ShareVibeX.Core.Settings;

public enum CaptureOutlineStyle { Dashed, Solid, Dotted }

public sealed record SelectionOptions
{
    public CaptureOutlineStyle OutlineStyle {get;init;}
    public string OutlineColor {get;init;}="#78D3FF";
    public int OutlineOpacity {get;init;}=100;
    public bool ShowRecordingOutline {get;init;}=true;
    public bool DetectWindows {get;init;}
    public bool DetectControls {get;init;}
    public bool ShowPosition {get;init;}
    public bool ShowSize {get;init;}=true;
    public bool ScreenCrosshair {get;init;}
    public bool CenterCrosshair {get;init;}
    public bool RestrictToActiveMonitor {get;init;}
    public int EditorDimPercent {get;init;}=53;
    public string InfoTemplate {get;init;}="{width} × {height} px";
    public string SnapSizes {get;init;}="640x480;1280x720;1920x1080";
}
