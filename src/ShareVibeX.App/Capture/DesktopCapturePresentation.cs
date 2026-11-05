using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;
using ShareVibeX.Platform.Windows;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Capture;

public sealed class DesktopCapturePresentation(App app, IDesktopCapture capture, ClipboardImageOwner clipboard,RecordingService? recording=null,
    Func<RecordingStartRequest,CancellationToken,Task>? recordStart=null,ShareVibeX.App.Notifications.CompletionNotificationService? feedback=null) : ICapturePresentation
{
    public Func<RecordingOptions,CancellationToken,Task<bool>>? MicrophoneProbe {get;init;}
    private ShareVibeX.App.Notifications.CompletionNotice? pendingNotice;
    private CapturedFrame? pendingPreview;
    private readonly WindowVisibilitySnapshot visibility = new();
    public bool RepeatRequested { get; private set; }

    public async Task HideAndWaitAsync(CancellationToken token)
    {
        RepeatRequested = false;pendingNotice=null;pendingPreview=null;feedback?.Block();
        app.History.Current=new(WindowsCaptureApplication.GetName(),DateTime.Now);
        var desktop = (IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!;
        visibility.Hide(desktop.Windows);
        DiagnosticLog.Write("Presentation: surfaces hidden");
        await Dispatcher.UIThread.InvokeAsync(() => { },DispatcherPriority.Background);
        DiagnosticLog.Write("Presentation: dispatcher ready");
        await Task.Run(WindowsDesktopCapture.FlushDesktop,token);
        DiagnosticLog.Write("Presentation: compositor ready");
        // Allow the desktop compositor to present the restored background before BitBlt.
        await Task.Delay(80,token);
    }

    public Task<CaptureSession?> SelectAsync(DesktopLayout layout, CancellationToken token) => new SelectionOverlayController(capture,clipboard,app.Settings,
        record:recordStart??(recording is null?null:recording.StartAsync),history:app.History,historyContext:app.History.Current,completed:(saved,path)=>pendingNotice=new(Loc.T(saved?"Screenshot saved":"Screenshot copied"),path,pendingPreview,FileName:ShareVibeX.App.History.CaptureHistory.FileName(app.History.Current,ShareVibeX.App.Capture.ImageEncoding.Extension(app.Settings.ImageFormat))),previewReady:frame=>{if(app.Settings.CompletionDefaults.ShowNotifications)pendingPreview=ShareVibeX.App.Notifications.CompletionPreview.CreateFrame(frame);}){MicrophoneProbe=MicrophoneProbe}.SelectAsync(layout,token);

    public Task ShowResultAsync(CaptureSession session, CancellationToken token) => Task.CompletedTask;

    public void Restore()
    {
        if (!app.IsExiting && recording?.IsActive!=true && recording?.HasPendingResult!=true)
        {visibility.Restore();feedback?.Unblock();if(pendingNotice is {} notice){pendingNotice=null;pendingPreview=null;feedback?.Publish(notice);}}
    }
    public void RestoreAfterRecording(){if(!app.IsExiting){visibility.Restore();feedback?.Unblock();}}

    public void ShowError(string message)
    {
        pendingNotice=null;pendingPreview=null;app.ReportStatus(message);
    }
}

