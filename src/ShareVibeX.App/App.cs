using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ShareVibeX.App.Themes;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Recording;
using ShareVibeX.Platform.Windows.Recording;

namespace ShareVibeX.App;

public sealed partial class App : Application
{
    private AppSettings settings = AppSettings.FactoryDefaults;
    private PortableSettingsStore store = null!;
    private SettingsWindow? settingsWindow;
    private IClassicDesktopStyleApplicationLifetime lifetime = null!;
    private TrayIcon? tray;
    private ShareVibeX.Platform.Windows.WindowsTrayIcon? windowsTray;
    private DispatcherTimer? trayTimer;
    private bool exiting;
    private bool settingsReady;
    private bool recordingActive;
    private bool recordingResultPending;
    private NativeMenuItem? stopRecordingMenu,captureMenu;
    private Window? recordingExitDialog;
    public Func<Task>? StopRecording {get;set;}
    public Func<Task<bool>>? PrepareExit {get;set;}
    public event Action? CaptureRequested;
    public History.CaptureHistory History {get;private set;}=null!;
    public event Action<AppSettings>? SettingsChanged;
    public AppSettings Settings => settings;
    public bool IsExiting => exiting;
    private string status = "Ready to capture";

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            lifetime = desktop;
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var args = Program.Arguments;
            var portable=ShareVibeX.Core.IO.PortablePaths.BaseDirectory;
            store=new PortableSettingsStore(Path.Combine(portable,"data"),Path.Combine(portable,"settings.json"));
            if (args.Contains("--diagnostics")) DiagnosticLog.DirectoryPath = store.DataDirectory;
            ThemeManager.Apply(this, settings.Theme);
            History=new History.CaptureHistory(()=>settings);
            PropertyChanged+=(_,e)=>{if(e.Property==RequestedThemeVariantProperty&&(tray is not null||windowsTray is not null)){DisposeTray();CreateTray();}};
            CreateTray();
            CompositionRoot.Attach(this,desktop);
            desktop.Startup += async (_,_) => await InitializeSettingsAsync();
            desktop.ShutdownRequested += (_,args) => {if(!exiting){args.Cancel=true;Exit();}};
            desktop.Exit += (_, _) => {DisposeTray();ThemeManager.Detach(this);};
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeSettingsAsync()
    {
        try
        {
            settings = await store.LoadAsync();
        }
        catch (Exception error)
        {
            ReportStatus("Portable settings need attention.");
            OpenRecovery(error.Message);
            return;
        }
        settingsReady = true;
        Loc.Language=settings.Language;DisposeTray();CreateTray();
        DiagnosticLog.Write("Portable settings loaded.");
        CapturePanelStyle.ButtonSize=40;ThemeManager.Apply(this,settings.Theme);
        try { SettingsChanged?.Invoke(settings); }
        catch (Exception error) { ReportStatus(error.Message); }
    }

    private void OpenRecovery(string error)
    {
        var window = new Window { Title = "ShareVibeX · Settings recovery", Width = 560, Height = 390,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Ui.WindowStyle(window);
        var text = Ui.Text(error);
        window.Content = new ScrollViewer { Margin = new Thickness(24), Content = Ui.Stack(
            Ui.Text("Choose how to continue", 22), text,
            Ui.Text("Settings use the folder beside ShareVibeX. If it is read-only, move the portable application to a writable location and restart. Existing settings are preserved.", muted: true),
            Ui.Button("Retry", async () =>
            {
                window.Close(); await InitializeSettingsAsync();
            }),
            Ui.Button("Back up and reset settings", async () =>
            {
                try
                {
                    var path = store.ExistingSettingsPath;
                    if (File.Exists(path)) File.Copy(path, path + $".{DateTime.Now:yyyyMMdd-HHmmss-fff}.bak", false);
                    await store.SaveAsync(AppSettings.FactoryDefaults);
                    window.Close(); await InitializeSettingsAsync();
                }
                catch (Exception failure) { text.Text = failure.Message; }
            }), Ui.Button("Exit", Exit)) };
        window.Show();
    }

    private void RequestCapture()
    {
        if (settingsReady) CaptureRequested?.Invoke();
        else ReportStatus("Resolve the settings error before capturing.");
    }

    internal bool TryCaptureRegisteredShortcut(string shortcut)=>settingsWindow?.TryCaptureRegisteredShortcut(shortcut)==true;

    private void OpenSettings()
    {
        if(exiting)return;
        var historyRequested=recordingActive||recordingResultPending;
        if (!settingsReady) { OpenRecovery(status); return; }
        if (settingsWindow is not null) { if(historyRequested)settingsWindow.SelectHistory();settingsWindow.RefreshEditAccess();settingsWindow.Show(); settingsWindow.Activate(); return; }
        var registration=new ShareVibeX.Platform.Windows.WindowsStartupRegistration(Environment.ProcessPath??Path.Combine(ShareVibeX.Core.IO.PortablePaths.BaseDirectory,"ShareVibeX.exe"));
        AppSettings displayed;
        try{displayed=settings with{StartWithWindows=registration.IsEnabled};}
        catch(Exception error){ReportStatus(Loc.F("Cannot read Windows startup preferences: {0}",Loc.T(error.Message)));return;}
        settingsWindow = new SettingsWindow(displayed, async proposed =>
        {
            await Startup.SettingsUpdate.SaveAsync(settings,displayed,proposed,
                value=>SettingsChanged?.Invoke(value),value=>store.SaveAsync(value),
                (enabled,persist)=>OperatingSystem.IsWindows()?Startup.StartupSettingsTransaction.SaveAsync(registration,enabled,persist):persist());
            var languageChanged=settings.Language!=proposed.Language;
            settings=displayed=proposed;
            if(languageChanged){Loc.Language=settings.Language;DisposeTray();CreateTray();}
            ReportStatus("Ready to capture");
        }, theme => ThemeManager.Apply(this, theme),OperatingSystem.IsWindows()?new WindowsRecordingAudioFactory():null,
            history:History,ffmpegPath:()=>Settings.RecordingDefaults.FfmpegPath,canEdit:()=>!recordingActive&&!recordingResultPending);
        if(historyRequested)settingsWindow.SelectHistory();
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.LanguageApplied+=()=>
        {
            settingsWindow?.Close();
            OpenSettings();
        };
        settingsWindow.SetStatus(status);
        settingsWindow.Show();
    }

    private string TrayIconUri => "avares://ShareVibeX.App/Assets/tray-"+(RequestedThemeVariant==Avalonia.Styling.ThemeVariant.Dark?"dark":"light")+".ico";

    private void CreateTray()
    {
        if(OperatingSystem.IsWindows())
        {
            try{CreateWindowsTray();return;}
            catch(Exception error){DiagnosticLog.Write("Native tray unavailable; using Avalonia fallback: "+error.Message);windowsTray?.Dispose();windowsTray=null;trayTimer?.Stop();}
        }
        using var stream = Avalonia.Platform.AssetLoader.Open(new Uri(TrayIconUri));
        tray = new TrayIcon { Icon = new WindowIcon(stream), ToolTipText = Loc.T("ShareVibeX · Capture region") };
        var menu = new NativeMenu();
        var capture = new NativeMenuItem(Loc.T("Capture region"));captureMenu=capture;capture.Click += (_, _) => RequestCapture();
        stopRecordingMenu=new NativeMenuItem(Loc.T("Stop recording")){IsVisible=false};stopRecordingMenu.Click+=async(_,_)=>
        {try{if(StopRecording is not null)await StopRecording();}catch(Exception error){ReportStatus(error.Message,true);}};
        var options = new NativeMenuItem(Loc.T("Settings")); options.Click += (_, _) => OpenSettings();
        var exit = new NativeMenuItem(Loc.T("Exit")); exit.Click += (_, _) => Exit();
        menu.Items.Add(capture);menu.Items.Add(stopRecordingMenu);menu.Items.Add(options);menu.Items.Add(new NativeMenuItemSeparator());menu.Items.Add(exit);
        tray.Menu = menu;
        tray.Clicked += (_, _) => RunTrayAction(settings.TraySingleClick);
        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }

    private void CreateWindowsTray()
    {
        using var stream=Avalonia.Platform.AssetLoader.Open(new Uri(TrayIconUri));using var bytes=new MemoryStream();stream.CopyTo(bytes);
        windowsTray=new(bytes.ToArray());
        var gestures=new TrayGestureRouter(ShareVibeX.Platform.Windows.WindowsTrayIcon.DoubleClickMilliseconds);
        trayTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        void Execute(TrayGesture gesture)=>RunTrayAction(gesture switch{TrayGesture.Single=>settings.TraySingleClick,TrayGesture.Double=>settings.TrayDoubleClick,_=>settings.TrayMiddleClick});
        trayTimer.Tick+=(_,_)=>{if(gestures.Advance(Environment.TickCount64) is {} gesture)Execute(gesture);};trayTimer.Start();
        windowsTray.Input+=message=>
        {
            switch(message)
            {
                case 0x202:gestures.LeftUp(Environment.TickCount64);break;
                case 0x203:Execute(gestures.DoubleClick());break;
                case 0x208:gestures.Cancel();Execute(TrayGesture.Middle);break;
                case 0x7b:gestures.Cancel();break;
                case 0x401:gestures.Cancel();Execute(TrayGesture.Single);break;
            }
        };
        windowsTray.Error+=error=>ReportStatus(Loc.F("Tray error: {0}",Loc.T(error.Message)));
        windowsTray.Menu=()=>new ShareVibeX.Platform.Windows.WindowsTrayMenuItem[]{
            new(Loc.T("Capture region"),!recordingActive&&!recordingResultPending,RequestCapture),
            new(Loc.T("Stop recording"),recordingActive,()=>RunTrayAction(TrayAction.StopRecording)),
            new(Loc.T("Settings"),true,OpenSettings),new(Loc.T("Exit"),true,Exit)};
    }
    private async void RunTrayAction(TrayAction action)
    {
        if(exiting)return;
        try
        {
            switch(action)
            {
                case TrayAction.None:break;
                case TrayAction.CaptureRegion:
                    if(recordingActive||recordingResultPending){ReportStatus("Close or stop the current recording first.");break;}RequestCapture();break;
                case TrayAction.OpenSettings:OpenSettings();break;
                case TrayAction.OpenHistory:OpenSettings();break;
                case TrayAction.StopRecording:if(recordingActive&&StopRecording is not null)await StopRecording();else ReportStatus("No recording is active.");break;
            }
        }
        catch(Exception error){ReportStatus(error.Message);}
    }
    private void DisposeTray(){trayTimer?.Stop();windowsTray?.Dispose();windowsTray=null;tray?.Dispose();tray=null;}

    public void ReportStatus(string message,bool showSettings=false)
    {
        message=Loc.T(message);status=message;windowsTray?.SetToolTip("ShareVibeX · "+message);
        if(tray is not null){var tip="ShareVibeX · "+message;tray.ToolTipText=tip[..Math.Min(127,tip.Length)];}
        settingsWindow?.SetStatus(message);
        if(showSettings && settingsReady && !exiting)OpenSettings();
    }

    public void Exit()
    {
        if (exiting) return;
        if(recordingActive)
        {
            if(recordingExitDialog is not null){recordingExitDialog.Activate();return;}
            var window=new Window{Title=Loc.T("ShareVibeX · Recording"),Width=470,Height=230,WindowDecorations=WindowDecorations.None,WindowStartupLocation=WindowStartupLocation.CenterScreen};
            recordingExitDialog=window;Ui.WindowStyle(window);var message=Ui.Text("Stop recording and review the result before exiting?",17);
            var saving=false;
            var controls=Ui.Stack(message,Ui.Button("Stop and review recording",async()=>
            {
                if(saving)return;saving=true;window.IsEnabled=false;message.Text=Loc.T("Finalizing recording…");
                try{if(StopRecording is not null)await StopRecording();saving=false;window.Close();CompleteExit();}
                catch(Exception error){message.Text=Loc.T(error.Message);}
                finally{saving=false;window.IsEnabled=true;}
            },true),Ui.Button("Continue recording",()=>window.Close()));controls.Margin=new Thickness(18);
            var body=new Grid{RowDefinitions=new("46,*"),Children={new ClientTitleBar(window),controls}};Grid.SetRow(controls,1);window.Content=body;
            window.Closing+=(_,args)=>{if(saving)args.Cancel=true;};
            window.Closed+=(_,_)=>recordingExitDialog=null;
            if(!ShareVibeX.Platform.Windows.WindowsCaptureExclusion.TryExclude(window.TryGetPlatformHandle()?.Handle??0))
            {window.Close();recordingExitDialog=null;ReportStatus("Use tray Stop recording before Exit; a capture-excluded confirmation window is unavailable.");return;}
            window.Show();return;
        }
        CompleteExit();
    }
    public void SetRecordingActive(bool value)
    {
        var changed=recordingActive!=value;
        recordingActive=value;
        if(changed)settingsWindow?.RefreshEditAccess();
        if(stopRecordingMenu is not null)stopRecordingMenu.IsVisible=value;
        if(captureMenu is not null)captureMenu.IsEnabled=!value&&!recordingResultPending;
    }
    public void SetRecordingResultPending(bool value)
    {
        var changed=recordingResultPending!=value;
        recordingResultPending=value;
        if(changed)settingsWindow?.RefreshEditAccess();
        if(captureMenu is not null)captureMenu.IsEnabled=!recordingActive&&!value;
    }
    private async void CompleteExit()
    {
        if(exiting)return;
        exiting = true;
        try{if(PrepareExit is not null&&!await PrepareExit()){exiting=false;return;}}
        catch(Exception error){exiting=false;ReportStatus(Loc.F("Exit cleanup failed: {0}",Loc.T(error.Message)));return;}
        DisposeTray();
        lifetime.Shutdown();
    }
}


