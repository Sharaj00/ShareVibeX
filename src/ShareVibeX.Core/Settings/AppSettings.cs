using ShareVibeX.Core.Recording;
using System.Text.Json.Serialization;

namespace ShareVibeX.Core.Settings;

public enum ThemeMode { System, Contrast }
public enum ImageFormat { Png, Jpeg, Webp }

public sealed record AppSettings(int SchemaVersion = 2, ThemeMode Theme = ThemeMode.System,
    string CaptureHotkey = "Ctrl+PrintScreen", string? OutputDirectory = null,
    ImageFormat ImageFormat = ImageFormat.Png, int ImageQuality = 90, RecordingOptions? Recording = null, int ButtonSize = 40, int RecordingCountdownSeconds = 0, bool HistoryEnabled = false, string? HistoryDirectory = null, bool OpenSettingsOnStartup = false, bool StartWithWindows = false, TrayAction TraySingleClick = TrayAction.CaptureRegion, TrayAction TrayDoubleClick = TrayAction.OpenSettings, TrayAction TrayMiddleClick = TrayAction.None, ShareVibeX.Core.Localization.AppLanguage Language = ShareVibeX.Core.Localization.AppLanguage.English, CompletionOptions? Completion = null, SelectionOptions? Selection = null, ShareVibeX.Core.Capture.CaptureBackend CaptureBackend = ShareVibeX.Core.Capture.CaptureBackend.Automatic, bool CreateMonthFolder = true)
{
    [JsonIgnore] public SelectionOptions SelectionDefaults => Selection ?? new SelectionOptions();
    [JsonIgnore] public CompletionOptions CompletionDefaults => Completion ?? new CompletionOptions();
    public static string DefaultCaptureDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) is {Length:>0} pictures
            ? pictures : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Pictures"),"Screenshots");
    [JsonIgnore] public string CaptureDirectory => OutputDirectory ?? HistoryDirectory ?? Recording?.OutputDirectory ?? DefaultCaptureDirectory;
    public string CaptureDirectoryFor(DateTime date)=>CreateMonthFolder
        ? Path.Combine(CaptureDirectory,date.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture))
        : CaptureDirectory;
    [JsonIgnore] public RecordingOptions RecordingDefaults => Recording ?? new RecordingOptions();
}


