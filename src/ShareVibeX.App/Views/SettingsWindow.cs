using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Security.Principal;
using Avalonia.Platform.Storage;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Views;

public sealed class SettingsWindow : Window
{
    private readonly TextBlock message=Ui.Text("",12,true);
    private Action selectHistory=()=>{},refreshEditAccess=()=>{};
    private readonly HotkeyEditor hotkey;
    internal bool TryCaptureRegisteredShortcut(string shortcut)=>hotkey.TryCaptureRegisteredShortcut(shortcut);
    public void SelectHistory()=>selectHistory();
    public void RefreshEditAccess()=>refreshEditAccess();
    internal event Action? LanguageApplied;
    public void SetStatus(string text)=>message.Text=Loc.T(text);

    public SettingsWindow(AppSettings settings,Func<AppSettings,Task> save,Action<ThemeMode> preview,IRecordingAudioFactory? audio=null,
        History.CaptureHistory? history=null,Func<string?>? ffmpegPath=null,Func<bool>? canEdit=null)
    {
        canEdit??=()=>true;
        Title=Loc.T("ShareVibeX · Settings");Width=840;Height=720;CanResize=false;
        var settingsScale=1d;
        if(Screens.Primary is {} screen)
        {
            settingsScale=Math.Min(1,Math.Min(screen.WorkingArea.Width/(Width*screen.Scaling),screen.WorkingArea.Height/(Height*screen.Scaling)));
            Width*=settingsScale;Height*=settingsScale;
        }
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=Brushes.Transparent;
        TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        TransparencyBackgroundFallback=Brushes.Transparent;
        WindowDecorations=WindowDecorations.None;
        var themeOptions=new[]{new ThemeOption("System",ThemeMode.System),new ThemeOption("Contrast (AMOLED)",ThemeMode.Contrast)};
        var theme=new ComboBox {ItemsSource=themeOptions,SelectedItem=themeOptions.First(option=>option.Mode==settings.Theme),HorizontalAlignment=HorizontalAlignment.Stretch};
        AutomationProperties.SetName(theme,"Theme");
        hotkey=new HotkeyEditor(settings.CaptureHotkey);Deactivated+=(_,_)=>hotkey.Cancel();
        var format=new ComboBox {ItemsSource=new[]{ImageFormat.Png,ImageFormat.Jpeg,ImageFormat.Webp},SelectedItem=settings.ImageFormat,HorizontalAlignment=HorizontalAlignment.Stretch};
        AutomationProperties.SetName(format,"Image format");
        var quality=new Slider {Minimum=1,Maximum=100,Value=settings.ImageQuality,TickFrequency=1,IsSnapToTickEnabled=true,VerticalAlignment=VerticalAlignment.Center};
        AutomationProperties.SetName(quality,"Image quality");
        var qualityValue=Ui.Text(settings.ImageQuality+"%",14);qualityValue.MinWidth=42;qualityValue.VerticalAlignment=VerticalAlignment.Center;
        var qualityGrid=new Grid {ColumnDefinitions=new("*,Auto"),ColumnSpacing=12,Children={quality,qualityValue}};Grid.SetColumn(qualityValue,1);
        var qualityHelp=Ui.Text("",12,true);
        void UpdateQuality(){qualityGrid.IsEnabled=format.SelectedItem is ImageFormat selected&&selected!=ImageFormat.Png;qualityHelp.Text=qualityGrid.IsEnabled?Loc.T("Higher quality keeps more detail and creates larger files."):Loc.T("PNG is lossless; quality applies to JPEG and WebP.");}
        format.SelectionChanged+=(_,_)=>UpdateQuality();quality.ValueChanged+=(_,_)=>qualityValue.Text=$"{Math.Round(quality.Value)}%";UpdateQuality();
        var output=new TextBox {Text=settings.OutputDirectory??settings.HistoryDirectory??settings.Recording?.OutputDirectory,PlaceholderText=AppSettings.DefaultCaptureDirectory,MinWidth=150};AutomationProperties.SetName(output,"Output folder");
        var browse=Ui.Button("…",async()=>
        {
            try
            {
                var folders=await StorageProvider.OpenFolderPickerAsync(new(){Title=Loc.T("Output folder"),AllowMultiple=false});
                if(folders.Count>0)output.Text=folders[0].TryGetLocalPath();
            }
            catch(Exception error){message.Text=Loc.T(error.Message);}
        });browse.Padding=new Thickness(10,7);AutomationProperties.SetName(browse,"Browse screenshot folder");
        var outputControls=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=6,Children={output,browse}};Grid.SetColumn(browse,1);
        var countdown=new NumericUpDown{Minimum=0,Maximum=10,Increment=1,Value=settings.RecordingCountdownSeconds};AutomationProperties.SetName(countdown,"Recording countdown");
        var historyEnabled=new CheckBox{IsChecked=settings.HistoryEnabled};AutomationProperties.SetName(historyEnabled,"Enable capture history");
        var monthFolder=new CheckBox{IsChecked=settings.CreateMonthFolder};AutomationProperties.SetName(monthFolder,"Create month folder");
        var autostart=new CheckBox{IsChecked=settings.StartWithWindows,IsEnabled=OperatingSystem.IsWindows()};AutomationProperties.SetName(autostart,"Run ShareVibeX when Windows starts");
        ComboBox TrayChoice(string name,TrayAction selected)
        {
            selected=selected==TrayAction.OpenHistory?TrayAction.OpenSettings:selected;
            var choices=new[]{new TrayOption("None",TrayAction.None),new TrayOption("Capture region",TrayAction.CaptureRegion),new TrayOption("Open settings",TrayAction.OpenSettings),new TrayOption("Stop recording",TrayAction.StopRecording)};
            var box=new ComboBox{ItemsSource=choices,SelectedItem=choices.Single(c=>c.Action==selected),HorizontalAlignment=HorizontalAlignment.Stretch};AutomationProperties.SetName(box,name);return box;
        }
        var single=TrayChoice("Tray single click",settings.TraySingleClick);var twice=TrayChoice("Tray double click",settings.TrayDoubleClick);var middle=TrayChoice("Tray middle click",settings.TrayMiddleClick);
        var language=new ComboBox{ItemsSource=new[]{"English","Русский"},SelectedIndex=(int)settings.Language,HorizontalAlignment=HorizontalAlignment.Stretch};AutomationProperties.SetName(language,"Interface language");
        var generalLeft=Page("Appearance",
            CompactRow("Language","Applies immediately.",language),CompactRow("Theme","Follow the Windows appearance and accent color, or use black contrast surfaces.",theme),
            CompactRow("Capture shortcut","Click the current shortcut to assign a new one. Esc cancels.",hotkey),CompactRow("Capture history","Automatically archive completed Copy/Save results by capture month.",historyEnabled),CompactRow("Output folder","Default folder for screenshots, videos and capture history.",outputControls),CompactRow("Create month folder","Save in a yyyy-MM subfolder.",monthFolder),CompactRow("Start with Windows","Applies to the current user and this portable executable.",autostart),Heading("Tray actions"),CompactRow("Left click","Waits for the system double-click interval.",single),CompactRow("Double left click","Executes once without the single-click action.",twice),CompactRow("Middle click","Right click always opens the tray menu.",middle));
        if(OperatingSystem.IsWindows())
        {
            using var identity=WindowsIdentity.GetCurrent();
            var elevated=new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            const string help="Windows may require matching administrator privileges to capture some elevated games and apps. If capture fails only while such an app is focused, close ShareVibeX and use Run as administrator.";
            var status=Ui.Text(elevated?"Running as administrator":"Running with standard privileges",13);
            ToolTip.SetTip(status,Loc.T(help));
            generalLeft.Children.Insert(2,CompactRow("Administrator privileges",help,status));
        }
        var completion=settings.CompletionDefaults;
        var completionSound=new CheckBox{IsChecked=completion.PlaySound};AutomationProperties.SetName(completionSound,"Play completion sound");
        var completionToast=new CheckBox{IsChecked=completion.ShowNotifications};AutomationProperties.SetName(completionToast,"Show completion notifications");
        var suppressFullscreen=new CheckBox{IsChecked=completion.SuppressFullscreen};AutomationProperties.SetName(suppressFullscreen,"Suppress in fullscreen");
        NumericUpDown NoticeNumber(string name,decimal minimum,decimal maximum,decimal value,decimal step=1)
        {var input=new NumericUpDown{Minimum=minimum,Maximum=maximum,Value=value,Increment=step};AutomationProperties.SetName(input,name);return input;}
        var duration=NoticeNumber("Notification duration",1,30,completion.DurationSeconds);var fade=NoticeNumber("Notification fade",0,5,(decimal)completion.FadeSeconds,0.1m);
        var noticeWidth=NoticeNumber("Notification width",240,800,completion.Width,10);var noticeHeight=NoticeNumber("Notification height",100,400,completion.Height,10);
        var placement=new ComboBox{ItemsSource=Enum.GetValues<CompletionPlacement>(),SelectedItem=completion.Placement,ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<CompletionPlacement>((value,_)=>Ui.Text(value switch{CompletionPlacement.BottomRight=>"Bottom right",CompletionPlacement.BottomLeft=>"Bottom left",CompletionPlacement.TopRight=>"Top right",_=>"Top left"}))};AutomationProperties.SetName(placement,"Notification placement");
        var customSound=new TextBox{Text=completion.CustomSoundPath??"",PlaceholderText=Loc.T("Windows system sound")};AutomationProperties.SetName(customSound,"Custom completion sound");
        var soundBrowse=Ui.Button("…",async()=>{try{var files=await StorageProvider.OpenFilePickerAsync(new(){Title=Loc.T("Completion sound"),AllowMultiple=false,FileTypeFilter=[new("WAV"){Patterns=["*.wav"]}]});if(files.Count>0)customSound.Text=files[0].TryGetLocalPath();}catch(Exception error){message.Text=Loc.T(error.Message);}});
        var soundPreview=Ui.Button("▶",()=>ShareVibeX.Platform.Windows.WindowsCompletionPlatform.PlayCompletionSound(customSound.Text));
        soundBrowse.Padding=soundPreview.Padding=new Thickness(10,6);AutomationProperties.SetName(soundBrowse,"Browse completion sound");AutomationProperties.SetName(soundPreview,"Preview completion sound");ToolTip.SetTip(soundBrowse,Loc.T("Browse…"));ToolTip.SetTip(soundPreview,Loc.T("Preview sound"));
        var soundControls=new Grid{ColumnDefinitions=new("*,Auto,Auto"),ColumnSpacing=4,Children={customSound,soundBrowse,soundPreview}};Grid.SetColumn(soundBrowse,1);Grid.SetColumn(soundPreview,2);
        ComboBox NoticeAction(string name,CompletionClickAction value)
        {
            var box=new ComboBox{ItemsSource=Enum.GetValues<CompletionClickAction>(),SelectedItem=value,HorizontalAlignment=HorizontalAlignment.Stretch,
                ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<CompletionClickAction>((item,_)=>Ui.Text(item switch{CompletionClickAction.None=>"None",CompletionClickAction.OpenFile=>"Open file",CompletionClickAction.OpenFolder=>"Open folder",_=>"Close notification"}))};AutomationProperties.SetName(box,name);return box;
        }
        var leftNotice=NoticeAction("Notification left action",completion.LeftAction);var rightNotice=NoticeAction("Notification right action",completion.RightAction);var middleNotice=NoticeAction("Notification middle action",completion.MiddleAction);
        var generalRight=Page("Completion",
            CompactRow("Sound","System sound after successful screenshot/video Copy or Save. Off by default.",completionSound),
            CompactRow("Custom sound","WAV file; empty uses the system sound. Relative paths use the portable application folder.",soundControls),
            CompactRow("Notifications","Completion notices are hidden during capture and recording.",completionToast));
        var notificationOptions=Ui.Stack(
            CompactRow("Duration (seconds)","Fade is included in the total display duration.",duration),CompactRow("Fade (seconds)","Cannot exceed the total duration.",fade),
            CompactRow("Width (pixels)","Clamped to the monitor working area.",noticeWidth),CompactRow("Height (pixels)","Clamped to the monitor working area.",noticeHeight),
            CompactRow("Placement","Uses the primary monitor working area.",placement),CompactRow("Fullscreen","Suppress sounds and notices during fullscreen Direct3D activity reported by Windows.",suppressFullscreen),CompactRow("Left click","File actions require an existing result file.",leftNotice),CompactRow("Right click","File actions require an existing result file.",rightNotice),CompactRow("Middle click","File actions require an existing result file.",middleNotice));
        AutomationProperties.SetName(notificationOptions,"Notification options");
        notificationOptions.IsVisible=completionToast.IsChecked==true;
        completionToast.IsCheckedChanged+=(_,_)=>notificationOptions.IsVisible=completionToast.IsChecked==true;
        notificationOptions.Spacing=4;
        generalRight.Children.Add(notificationOptions);
        var general=new Grid{ColumnDefinitions=new("*,*"),ColumnSpacing=12,Children={generalLeft,generalRight}};
        Grid.SetColumn(generalRight,1);
        StackPanel MediaField(string title,Control value,Control help)
        {
            var field=new StackPanel{Spacing=2,Children={Ui.Text(title,12),value}};
            ToolTip.SetTip(field,help);
            return field;
        }
        var mediaOptions=new Grid{ColumnDefinitions=new("*,*,*"),ColumnSpacing=10};
        var fields=new[]
        {
            MediaField("File format",format,Ui.Text("Default format and filename extension for new screenshots.",12,true)),
            MediaField("Image quality",qualityGrid,qualityHelp),
            MediaField("Recording countdown",countdown,Ui.Text("Seconds before recording starts (0 disables the countdown).",12,true))
        };
        for(var i=0;i<fields.Length;i++)
        {
            Grid.SetColumn(fields[i],i);
            mediaOptions.Children.Add(fields[i]);
        }
        var video=new VideoSettingsPage(settings.RecordingDefaults,audio:audio,captureBackend:settings.CaptureBackend);
        var media=new StackPanel{Spacing=8,Children={mediaOptions,video}};
        var captureSettings=new CaptureSettingsPage(settings.SelectionDefaults);
        var historyPage=new History.CaptureHistoryPage(history??new(()=>settings),ffmpegPath);
        var captureTabs=new TabControl{ItemsSource=new[]{
            new TabItem{Header=Loc.T("Media"),Content=media},
            new TabItem{Header=Loc.T("Selection"),Content=captureSettings}},SelectedIndex=0};
        AutomationProperties.SetName(captureTabs,"Capture settings tabs");
        const int historyIndex=2;
        TabItem SettingsTab(string title,Control page)=>new(){Header=Loc.T(title),
            Content=new Border{Child=page is History.CaptureHistoryPage?new ScrollViewer{Content=page}:page,Margin=new Thickness(10,4,10,6)}};
        var entries=new[]{SettingsTab("General",general),SettingsTab("Capture",captureTabs),SettingsTab("History",historyPage)};
        var navigation=new TabControl{ItemsSource=entries,SelectedIndex=0,Margin=new Thickness(12,0)};
        AutomationProperties.SetName(navigation,"Settings sections");

        void UpdateNavigation()
        {
            var editable=canEdit();
            if(!editable&&navigation.SelectedIndex!=historyIndex){navigation.SelectedIndex=historyIndex;return;}
            if(navigation.SelectedIndex==historyIndex&&IsVisible)_=historyPage.ActivateAsync();else historyPage.Deactivate();
            for(var index=0;index<historyIndex;index++)entries[index].IsEnabled=editable;
        }
        navigation.SelectionChanged+=(_,args)=>{if(ReferenceEquals(args.Source,navigation))UpdateNavigation();};UpdateNavigation();
        selectHistory=()=>{if(navigation.SelectedIndex==historyIndex)UpdateNavigation();else navigation.SelectedIndex=historyIndex;};
        refreshEditAccess=UpdateNavigation;Opened+=(_,_)=>UpdateNavigation();
        Activated+=(_,_)=>UpdateNavigation();
        PropertyChanged+=(_,args)=>{if(args.Property==IsVisibleProperty){if(IsVisible)UpdateNavigation();else historyPage.Deactivate();}};
        var windowLanguage=settings.Language;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
        var saving=false;
        var pending=false;
        var closeRequested=false;
        var closed=false;
        void QueueSave()
        {
            if(closed||!canEdit())return;
            pending=true;
            timer.Stop();
            timer.Start();
        }
        async Task ApplyChanges()
        {
            timer.Stop();
            if(saving||!pending||closed)return;
            saving=true;
            var succeeded=false;
            try
            {
                while(pending&&!closed)
                {
                    pending=false;
                    if(!canEdit())throw new InvalidOperationException(Loc.T("Close the recording session before changing settings."));
                    var next=settings with
                    {
                        CaptureBackend=video.GetCaptureBackend(),
                        Selection=captureSettings.GetOptions(),
                        Completion=new CompletionOptions
                        {
                            CustomSoundPath=string.IsNullOrWhiteSpace(customSound.Text)?null:customSound.Text.Trim(),
                            LeftAction=(CompletionClickAction)leftNotice.SelectedItem!,
                            RightAction=(CompletionClickAction)rightNotice.SelectedItem!,
                            MiddleAction=(CompletionClickAction)middleNotice.SelectedItem!,
                            PlaySound=completionSound.IsChecked==true,
                            ShowNotifications=completionToast.IsChecked==true,
                            SuppressFullscreen=suppressFullscreen.IsChecked==true,
                            DurationSeconds=(int)(duration.Value??3),
                            FadeSeconds=(double)(fade.Value??1),
                            Width=(int)(noticeWidth.Value??360),
                            Height=(int)(noticeHeight.Value??140),
                            Placement=(CompletionPlacement)placement.SelectedItem!
                        },
                        Language=(AppLanguage)language.SelectedIndex,
                        TraySingleClick=((TrayOption)single.SelectedItem!).Action,
                        TrayDoubleClick=((TrayOption)twice.SelectedItem!).Action,
                        TrayMiddleClick=((TrayOption)middle.SelectedItem!).Action,
                        StartWithWindows=autostart.IsChecked==true,
                        OpenSettingsOnStartup=false,
                        HistoryEnabled=historyEnabled.IsChecked==true,
                        HistoryDirectory=null,
                        CreateMonthFolder=monthFolder.IsChecked==true,
                        ButtonSize=40,
                        RecordingCountdownSeconds=(int)(countdown.Value??0),
                        Theme=((ThemeOption)theme.SelectedItem!).Mode,
                        CaptureHotkey=hotkey.Shortcut,
                        OutputDirectory=string.IsNullOrWhiteSpace(output.Text)?null:output.Text.Trim(),
                        ImageFormat=(ImageFormat)format.SelectedItem!,
                        ImageQuality=(int)Math.Round(quality.Value),
                        Recording=video.GetOptions() with{OutputDirectory=null}
                    };
                    if(next==settings)continue;
                    await save(next);
                    if(next.Theme!=settings.Theme)preview(next.Theme);
                    settings=next;
                    message.Text="";
                }
                succeeded=true;
            }
            catch(Exception error){closeRequested=false;message.Text=Loc.T(error.Message);}
            finally
            {
                saving=false;
                if(pending&&!closed)timer.Start();
                else if(closeRequested&&!closed)Close();
                else if(succeeded&&!closed&&settings.Language!=windowLanguage)LanguageApplied?.Invoke();
            }
        }
        timer.Tick+=async(_,_)=>await ApplyChanges();
        foreach(var choice in new[]{theme,format,language,single,twice,middle,placement,leftNotice,rightNotice,middleNotice})
            choice.SelectionChanged+=(_,_)=>{if(choice.SelectedItem is not null)QueueSave();};
        foreach(var check in new[]{historyEnabled,monthFolder,autostart,completionSound,completionToast,suppressFullscreen})
            check.IsCheckedChanged+=(_,_)=>QueueSave();
        foreach(var number in new[]{countdown,duration,fade,noticeWidth,noticeHeight})
            number.ValueChanged+=(_,_)=>{if(number.Value is not null)QueueSave();};
        foreach(var input in new[]{output,customSound})
            input.PropertyChanged+=(_,e)=>{if(e.Property==TextBox.TextProperty)QueueSave();};
        quality.ValueChanged+=(_,_)=>QueueSave();
        hotkey.ShortcutChanged+=QueueSave;
        video.SettingsChanged+=QueueSave;
        captureSettings.SettingsChanged+=QueueSave;

        var footer=new Border {Margin=new Thickness(22,4,22,8),Child=message};
        Grid.SetRow(navigation,1);Grid.SetRow(footer,2);
        var body=new Grid{RowDefinitions=new("46,*,Auto"),Children={new ClientTitleBar(this,showMaximize:false),navigation,footer}};
        var shell=new Border{CornerRadius=new CornerRadius(12),Child=body};
        shell.Bind(Border.BackgroundProperty,this.GetResourceObservable("WindowBackground"));
        shell.SizeChanged+=(_,args)=>shell.Clip=new RectangleGeometry(new Rect(args.NewSize),12,12);
        if(settingsScale<1)
        {
            shell.Width=840;shell.Height=720;
            Content=new Viewbox{Stretch=Stretch.Uniform,Child=shell};
        }
        else Content=shell;
        Closing+=(_,args)=>
        {
            if((saving||pending)&&args.CloseReason==WindowCloseReason.WindowClosing)
            {
                args.Cancel=true;
                closeRequested=true;
                Dispatcher.UIThread.Post(async()=>await ApplyChanges());
            }
        };
        Closed+=(_,_)=>{closed=true;timer.Stop();historyPage.Dispose();video.Dispose();};
    }

    internal static StackPanel Page(string section,params Control[] rows)
    {
        var panel=new StackPanel{Spacing=4};panel.Children.Add(Heading(section));
        foreach(var row in rows)panel.Children.Add(row);return panel;
    }
    internal static TextBlock Heading(string text)=>new(){Text=Loc.T(text),FontSize=16,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,6,0,2)};
    internal static Border InfoCard(string text)=>Card(Ui.Text(text,15,true));
    internal static Border InfoCard(Control content)=>Card(content);
    internal static Border Row(string title,string description,Control value)=>Row(title,Ui.Text(description,12,true),value);
    private static Border CompactRow(string title,string description,Control value)=>Row(title,Ui.Text(description,12,true),value,compact:true);
    private static Border Row(string title,Control description,Control value,bool compact=false)
    {
        var label=Ui.Text(title,compact?13:15);label.VerticalAlignment=VerticalAlignment.Center;
        var grid=new Grid {ColumnDefinitions=new(compact?"*,1.25*":"*,260"),ColumnSpacing=8,Children={label,value}};
        Grid.SetColumn(value,1);value.VerticalAlignment=VerticalAlignment.Center;
        var card=Card(grid);card.Padding=compact?new Thickness(8,1):new Thickness(12,6);
        if(compact)card.Height=42;
        ToolTip.SetTip(card,description);
        if(value is CheckBox && description is TextBlock hint)ToolTip.SetTip(value,hint.Text);return card;
    }
    private static Border Card(Control content)
    {
        var card=Ui.Card(content);card.CornerRadius=new CornerRadius(6);card.Padding=new Thickness(18,20);return card;
    }
    private sealed record TrayOption(string Label,TrayAction Action){public override string ToString()=>Loc.T(Label);}
    private sealed record ThemeOption(string Label,ThemeMode Mode){public override string ToString()=>Loc.T(Label);}
}




