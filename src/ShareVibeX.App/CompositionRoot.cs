using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows;
using ShareVibeX.App.Recording;
using ShareVibeX.Platform.Windows.Recording;
using ShareVibeX.App.Playback;
using ShareVibeX.Platform.Windows.Playback;


namespace ShareVibeX.App;

public static class CompositionRoot
{
    public static void Attach(App app, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        if (!OperatingSystem.IsWindows())
        {
            app.ReportStatus(Loc.T("Screen capture is currently available on Windows. Linux support is planned."));
            return;
        }
        var feedback=new ShareVibeX.App.Notifications.CompletionNotificationService(()=>app.Settings.CompletionDefaults);
        var capture = new ScreenshotCaptureRouter(new ShareVibeX.Platform.Windows.Capture.WindowsGraphicsDesktopCapture(),
            new WindowsDesktopCapture(),()=>app.Settings.CaptureBackend,report=>
            {
                var method=report.EffectiveBackend==CaptureBackend.WindowsGraphicsCapture?"Windows Graphics Capture":"GDI";
                var message=report.FallbackReason is null?Loc.F("Screenshot source: {0}.",method):Loc.F("Screenshot source: GDI. WGC unavailable: {0}",report.FallbackReason);
                DiagnosticLog.Write(message);Dispatcher.UIThread.Post(()=>app.ReportStatus(message));
            });
        var clipboard = new ClipboardImageOwner();
        var recording=new RecordingService(new RecordingEngine(new ProcessCommandRunner(),new FfmpegRecordingProcessFactory(),new WindowsRecordingAudioFactory(),
            resolveWgc:new WindowsWgcVideoSourceResolver().Resolve,
            openMovableVideo:(region,monitor,options,token)=>new WindowsMovableRecordingVideo(region,monitor,options,token)),preference:()=>app.Settings.CaptureBackend);
        var preview=new RecordingPreviewHost(new FfmpegPreviewDecoderFactory(),new WindowsPreviewAudioOutputFactory(),app.History,feedback.Publish);
        var microphones=new MicrophoneAvailability(new WindowsRecordingAudioFactory());
        var recordingControls=new RecordingControlPresenter(recording,capture.GetMonitors,preview:preview){MicrophoneProbe=microphones.CheckAsync};
        var presentation = new DesktopCapturePresentation(app,capture,clipboard,recording,recordingControls.StartAsync,feedback){MicrophoneProbe=microphones.CheckAsync};
        var elapsed=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};
        var owned=false;
        elapsed.Tick+=(_,_)=>{if(recording.IsActive)app.ReportStatus(recording.Status+Loc.T(" · Stop: ")+app.Settings.CaptureHotkey+Loc.T(" / tray"));};
        recording.Changed+=()=>
        {
            void Apply()
            {
            if(recording.IsActive)feedback.Block();else if(recording.HasPendingResult)feedback.Unblock();
            app.SetRecordingActive(recording.IsActive);app.SetRecordingResultPending(recording.HasPendingResult);
            if(recording.IsActive){elapsed.Start();app.ReportStatus(recording.Status);}else elapsed.Stop();
            var currentOwnership=recording.IsActive||recording.HasPendingResult;
            if(owned&&!currentOwnership)presentation.RestoreAfterRecording();owned=currentOwnership;
            }
            if(Dispatcher.UIThread.CheckAccess())Apply();else Dispatcher.UIThread.Post(Apply);
        };
        recording.Finished+=outcome=>Dispatcher.UIThread.Post(()=>
        {
            try{if(!recording.HasPendingResult)presentation.RestoreAfterRecording();}catch(Exception error){DiagnosticLog.Write("Recording restore failed: "+error.Message);}
            if(outcome.Canceled)app.ReportStatus(Loc.T("Recording canceled."));
            else if(outcome.Result is {} result)app.ReportStatus(RecordingService.Describe(result));
        });
        app.StopRecording=recording.StopAsync;
        app.PrepareExit=recordingControls.RequestExitAsync;
        var workflow = new CaptureWorkflow(new CaptureCoordinator(),capture,presentation);
        WindowsGlobalHotkey? hotkey = null;
        try { hotkey = new WindowsGlobalHotkey(); }
        catch (Exception error) { app.ReportStatus(error.Message); }
        var running = false;
        async void Start()
        {
            if(recording.IsActive){try{await recording.StopAsync();}catch(Exception error){app.ReportStatus(error.Message);}return;}
            if(recording.HasPendingResult){recordingControls.Activate();return;}
            if (running || app.IsExiting) return;
            running = true;
            try { do { await workflow.StartAsync(); } while (presentation.RepeatRequested && !app.IsExiting); }
            catch(Exception error){DiagnosticLog.Write("Capture entry failed: "+error);app.ReportStatus(error.Message);}
            finally { running = false; }
        }
        app.CaptureRequested += Start;
        app.SettingsChanged += settings =>
        {
                        hotkey?.Register(settings.CaptureHotkey,() =>
            {
                if (!app.TryCaptureRegisteredShortcut(settings.CaptureHotkey))
                    Dispatcher.UIThread.Post(Start);
            });
        };
        lifetime.ShutdownRequested += (_, _) => workflow.Cancel();
        lifetime.Exit += (_, _) => { WindowsSelectionTools.ReleaseCursorConstraints();feedback.Dispose();elapsed.Stop();recordingControls.Dispose();workflow.Cancel(); hotkey?.Dispose(); clipboard.Dispose(); };
    }
}


