using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
    public void SetStatus(string text)=>message.Text=Loc.T(text);

    public SettingsWindow(AppSettings settings,Func<AppSettings,Task> save,Action<ThemeMode> preview,IRecordingAudioFactory? audio=null,
        History.CaptureHistory? history=null,Func<string?>? ffmpegPath=null,Func<bool>? canEdit=null)
    {
        canEdit??=()=>true;
        Title=Loc.T("ShareVibeX · Settings");Width=1100;Height=760;MinWidth=920;MinHeight=580;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;Ui.WindowStyle(this);
        WindowDecorations=WindowDecorations.None;ClientTitleBar.EnableEdgeResize(this);
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
        void UpdateQuality(){qualityGrid.IsEnabled=(ImageFormat)format.SelectedItem! !=ImageFormat.Png;qualityHelp.Text=qualityGrid.IsEnabled?Loc.T("Higher quality keeps more detail and creates larger files."):Loc.T("PNG is lossless; quality applies to JPEG and WebP.");}
        format.SelectionChanged+=(_,_)=>UpdateQuality();quality.ValueChanged+=(_,_)=>qualityValue.Text=$"{Math.Round(quality.Value)}%";UpdateQuality();
        var output=new TextBox {Text=settings.CaptureDirectory,PlaceholderText=AppSettings.DefaultCaptureDirectory,MinWidth=150};AutomationProperties.SetName(output,"Output folder");
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
        var historyEnabled=new CheckBox{Content=Loc.T("Enable capture history"),IsChecked=settings.HistoryEnabled};AutomationProperties.SetName(historyEnabled,"Enable capture history");
        var monthFolder=new CheckBox{IsChecked=settings.CreateMonthFolder};AutomationProperties.SetName(monthFolder,"Create month folder");
        var autostart=new CheckBox{Content=Loc.T("Run ShareVibeX when Windows starts"),IsChecked=settings.StartWithWindows,IsEnabled=OperatingSystem.IsWindows()};AutomationProperties.SetName(autostart,"Run ShareVibeX when Windows starts");
        ComboBox TrayChoice(string name,TrayAction selected)
        {
            selected=selected==TrayAction.OpenHistory?TrayAction.OpenSettings:selected;
            var choices=new[]{new TrayOption("None",TrayAction.None),new TrayOption("Capture region",TrayAction.CaptureRegion),new TrayOption("Open settings",TrayAction.OpenSettings),new TrayOption("Stop recording",TrayAction.StopRecording)};
            var box=new ComboBox{ItemsSource=choices,SelectedItem=choices.Single(c=>c.Action==selected),HorizontalAlignment=HorizontalAlignment.Stretch};AutomationProperties.SetName(box,name);return box;
        }
        var single=TrayChoice("Tray single click",settings.TraySingleClick);var twice=TrayChoice("Tray double click",settings.TrayDoubleClick);var middle=TrayChoice("Tray middle click",settings.TrayMiddleClick);
        var language=new ComboBox{ItemsSource=new[]{"English","Русский"},SelectedIndex=(int)settings.Language,HorizontalAlignment=HorizontalAlignment.Stretch};AutomationProperties.SetName(language,"Interface language");
        var general=Page("Appearance",
            Row("Language","Applies after saving settings.",language),Row("Theme","Follow the Windows appearance and accent color, or use black contrast surfaces.",theme),
            Row("Recording countdown","Seconds before recording starts (0 disables the countdown).",countdown),Heading("Capture"),Row("Capture shortcut","Click the current shortcut to assign a new one. Esc cancels.",hotkey),Heading("History"),Row("Capture history","Automatically archive completed Copy/Save results by capture month.",historyEnabled),Row("Output folder","Default folder for screenshots, videos and capture history.",outputControls),Row("Create month folder","Save in a yyyy-MM subfolder.",monthFolder),Heading("Startup"),Row("Start with Windows","Applies to the current user and this portable executable when settings are saved.",autostart),Heading("Tray actions"),Row("Left click","Waits for the system double-click interval.",single),Row("Double left click","Executes once without the single-click action.",twice),Row("Middle click","Right click always opens the tray menu.",middle));
        var completion=settings.CompletionDefaults;
        var completionSound=new CheckBox{Content=Loc.T("Play completion sound"),IsChecked=completion.PlaySound};AutomationProperties.SetName(completionSound,"Play completion sound");
        var completionToast=new CheckBox{IsChecked=completion.ShowNotifications};AutomationProperties.SetName(completionToast,"Show completion notifications");
        var suppressFullscreen=new CheckBox{Content=Loc.T("Suppress in exclusive fullscreen"),IsChecked=completion.SuppressFullscreen};AutomationProperties.SetName(suppressFullscreen,"Suppress in fullscreen");
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
        general.Children.Add(Heading("Completion"));general.Children.Add(Row("Sound","System sound after successful screenshot/video Copy or Save. Off by default.",completionSound));
        general.Children.Add(Row("Custom sound","WAV file; empty uses the system sound. Relative paths use the portable application folder.",soundControls));
        general.Children.Add(Row("Notifications","Completion notices are hidden during capture and recording.",completionToast));
        var notificationOptions=Ui.Stack(
            Row("Duration (seconds)","Fade is included in the total display duration.",duration),Row("Fade (seconds)","Cannot exceed the total duration.",fade),
            Row("Width (pixels)","Clamped to the monitor working area.",noticeWidth),Row("Height (pixels)","Clamped to the monitor working area.",noticeHeight),
            Row("Placement","Uses the primary monitor working area.",placement),Row("Fullscreen","Suppress sounds and notices during fullscreen Direct3D activity reported by Windows.",suppressFullscreen),Row("Left click","File actions require an existing result file.",leftNotice),Row("Right click","File actions require an existing result file.",rightNotice),Row("Middle click","File actions require an existing result file.",middleNotice));
        AutomationProperties.SetName(notificationOptions,"Notification options");
        notificationOptions.IsVisible=completionToast.IsChecked==true;
        completionToast.IsCheckedChanged+=(_,_)=>notificationOptions.IsVisible=completionToast.IsChecked==true;
        general.Children.Add(notificationOptions);
        var image=Page("Saving",
            Row("File format","Default format and filename extension for new screenshots.",format),
            Row("Image quality",qualityHelp,qualityGrid));
        var video=new VideoSettingsPage(settings.RecordingDefaults,audio:audio,captureBackend:settings.CaptureBackend);
        var captureSettings=new CaptureSettingsPage(settings.SelectionDefaults);
        var historyPage=new History.CaptureHistoryPage(history??new(()=>settings),ffmpegPath);
        var captureTabs=new TabControl{ItemsSource=new[]{
            new TabItem{Header=Loc.T("Image"),Content=image},
            new TabItem{Header=Loc.T("Video"),Content=video},
            new TabItem{Header=Loc.T("Selection"),Content=captureSettings}},SelectedIndex=1};
        AutomationProperties.SetName(captureTabs,"Capture settings tabs");
        const int historyIndex=2;
        TabItem SettingsTab(string title,Control page)=>new(){Header=Loc.T(title),
            Content=new ScrollViewer{Content=page,Margin=new Thickness(26,10,30,18)}};
        var entries=new[]{SettingsTab("General",general),SettingsTab("Capture",captureTabs),SettingsTab("History",historyPage)};
        var navigation=new TabControl{ItemsSource=entries,SelectedIndex=0,Margin=new Thickness(12,0)};
        AutomationProperties.SetName(navigation,"Settings sections");
        Button? saveButton=null;
        void UpdateNavigation()
        {
            var editable=canEdit();
            if(!editable&&navigation.SelectedIndex!=historyIndex){navigation.SelectedIndex=historyIndex;return;}
            if(navigation.SelectedIndex==historyIndex&&IsVisible)_=historyPage.ActivateAsync();else historyPage.Deactivate();
            for(var index=0;index<historyIndex;index++)entries[index].IsEnabled=editable;
            if(saveButton is not null)saveButton.IsVisible=navigation.SelectedIndex!=historyIndex;

        }
        navigation.SelectionChanged+=(_,args)=>{if(ReferenceEquals(args.Source,navigation))UpdateNavigation();};UpdateNavigation();
        selectHistory=()=>{if(navigation.SelectedIndex==historyIndex)UpdateNavigation();else navigation.SelectedIndex=historyIndex;};
        refreshEditAccess=UpdateNavigation;Opened+=(_,_)=>UpdateNavigation();
        Activated+=(_,_)=>UpdateNavigation();
        PropertyChanged+=(_,args)=>{if(args.Property==IsVisibleProperty){if(IsVisible)UpdateNavigation();else historyPage.Deactivate();}};
        var committed=false;var saving=false;
        saveButton=Ui.Button("Save settings",async()=>
        {
            if(!canEdit()){message.Text=Loc.T("Close the recording session before changing settings.");return;}
            if(saving)return;saving=true;message.Text=Loc.T("Saving settings. Please wait.");navigation.IsEnabled=false;
            try
            {
                await save(settings with {CaptureBackend=video.GetCaptureBackend(),Selection=captureSettings.GetOptions(),Completion=new CompletionOptions{CustomSoundPath=string.IsNullOrWhiteSpace(customSound.Text)?null:customSound.Text.Trim(),LeftAction=(CompletionClickAction)leftNotice.SelectedItem!,RightAction=(CompletionClickAction)rightNotice.SelectedItem!,MiddleAction=(CompletionClickAction)middleNotice.SelectedItem!,PlaySound=completionSound.IsChecked==true,ShowNotifications=completionToast.IsChecked==true,SuppressFullscreen=suppressFullscreen.IsChecked==true,DurationSeconds=(int)(duration.Value??3),FadeSeconds=(double)(fade.Value??1),Width=(int)(noticeWidth.Value??360),Height=(int)(noticeHeight.Value??140),Placement=(CompletionPlacement)placement.SelectedItem!},Language=(AppLanguage)language.SelectedIndex,TraySingleClick=((TrayOption)single.SelectedItem!).Action,TrayDoubleClick=((TrayOption)twice.SelectedItem!).Action,TrayMiddleClick=((TrayOption)middle.SelectedItem!).Action,StartWithWindows=autostart.IsChecked==true,OpenSettingsOnStartup=false,HistoryEnabled=historyEnabled.IsChecked==true,HistoryDirectory=null,CreateMonthFolder=monthFolder.IsChecked==true,ButtonSize=40,RecordingCountdownSeconds=(int)(countdown.Value??0),Theme=((ThemeOption)theme.SelectedItem!).Mode,CaptureHotkey=hotkey.Shortcut,
                    OutputDirectory=string.IsNullOrWhiteSpace(output.Text)?null:output.Text.Trim(),ImageFormat=(ImageFormat)format.SelectedItem!,ImageQuality=(int)Math.Round(quality.Value),Recording=video.GetOptions() with{OutputDirectory=null}});
                committed=true;Close();
            }
            catch(Exception error){message.Text=Loc.T(error.Message);}
            finally{saving=false;navigation.IsEnabled=true;}
        },true);
        AutomationProperties.SetName(saveButton,"Save settings");saveButton.IsVisible=navigation.SelectedIndex!=historyIndex;
        var footer=new Grid {ColumnDefinitions=new("*,Auto"),ColumnSpacing=18,Margin=new Thickness(26,8,30,20),Children={message,saveButton}};
        Grid.SetColumn(saveButton,1);message.VerticalAlignment=VerticalAlignment.Center;
        Grid.SetRow(navigation,1);Grid.SetRow(footer,2);
        Content=new Grid{RowDefinitions=new("46,*,Auto"),Children={new ClientTitleBar(this),navigation,footer}};
                theme.SelectionChanged+=(_,_)=>{if(theme.SelectedItem is ThemeOption option)preview(option.Mode);};
        Closing+=(_,args)=>
        {
            if(saving && !committed && args.CloseReason==WindowCloseReason.WindowClosing)
            {args.Cancel=true;message.Text=Loc.T("Saving settings. Please wait.");}
        };
        Closed+=(_,_)=>{historyPage.Dispose();video.Dispose();if(!committed)preview(settings.Theme);};
    }

    internal static StackPanel Page(string section,params Control[] rows)
    {
        var panel=new StackPanel{Spacing=8};panel.Children.Add(Heading(section));
        foreach(var row in rows)panel.Children.Add(row);return panel;
    }
    internal static TextBlock Heading(string text)=>new(){Text=Loc.T(text),FontSize=16,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,22,0,6)};
    internal static Border InfoCard(string text)=>Card(Ui.Text(text,15,true));
    internal static Border InfoCard(Control content)=>Card(content);
    internal static Border Row(string title,string description,Control value)=>Row(title,Ui.Text(description,12,true),value);
    private static Border Row(string title,Control description,Control value)
    {
        var label=Ui.Text(title,15);label.VerticalAlignment=VerticalAlignment.Center;
        var grid=new Grid {ColumnDefinitions=new("*,260"),ColumnSpacing=12,Children={label,value}};
        Grid.SetColumn(value,1);value.VerticalAlignment=VerticalAlignment.Center;
        var card=Card(grid);card.Padding=new Thickness(12,6);ToolTip.SetTip(card,description);return card;
    }
    private static Border Card(Control content)
    {
        var card=Ui.Card(content);card.CornerRadius=new CornerRadius(6);card.Padding=new Thickness(18,20);return card;
    }
    private sealed record TrayOption(string Label,TrayAction Action){public override string ToString()=>Loc.T(Label);}
    private sealed record ThemeOption(string Label,ThemeMode Mode){public override string ToString()=>Loc.T(Label);}
}




