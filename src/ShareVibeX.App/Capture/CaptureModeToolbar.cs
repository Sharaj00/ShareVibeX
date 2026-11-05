using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using ShareVibeX.Core.Recording;
using ShareVibeX.App.Editing;
using ShareVibeX.App.Views;

namespace ShareVibeX.App.Capture;

public sealed class CaptureModeToolbar : Border
{
    private readonly Control choosing, video;
    internal RegionMoveGrip MoveGrip {get;}=new();
    private readonly TextBlock countdownValue=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private int countdownSeconds;
    public void SetCountdown(int seconds){countdownSeconds=seconds;countdownValue.Text=Loc.F("{0}s",seconds);}
    private readonly TextBlock info = Ui.Text("", 12);
    public TextBlock Status { get; } = Ui.Text("", 12);
    private readonly ToggleButton sound,microphone,cursor;
    private bool syncing;
    private readonly bool animation;
    private bool microphoneAvailable=true;
    public void SetMicrophoneAvailable(bool available)
    {
        microphoneAvailable=available;microphone.IsEnabled=available&&!animation;
        if(!available)SetSources(Sources with{Microphone=false});
        ToolTip.SetTip(microphone,Loc.T(animation?"Animations are silent.":available?"Microphone":ShareVibeX.App.Recording.MicrophoneAvailability.UnavailableMessage));
        ToolTip.SetShowOnDisabled(microphone,true);
    }
    public RecordingSourceChoices Sources=>new(sound.IsChecked==true,microphone.IsChecked==true);
    public void SetSources(RecordingSourceChoices sources)
    {
        syncing=true;sound.IsChecked=!animation&&sources.Sound;microphone.IsChecked=!animation&&microphoneAvailable&&sources.Microphone;syncing=false;
    }
    public void SetCursorIncluded(bool included)
    {
        syncing=true;cursor.IsChecked=included;syncing=false;
    }
    public CaptureModeToolbar(Func<Task> edit, Action selectVideo, Action back, Action cancel,Func<RecordingSourceChoices,Task>? record=null,
        RecordingSourceChoices? sources=null,Action<RecordingSourceChoices>? sourcesChanged=null,bool directShow=false,bool animation=false,
        bool includeCursor=true,Action<bool>? cursorChanged=null,int countdownSeconds=0,Action<int>? countdownChanged=null)
    {
        this.animation=animation;
        Classes.Add("card");CapturePanelStyle.Apply(this); Padding = new Thickness(8); CornerRadius = new CornerRadius(8);
        choosing = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Spacing = 4, Children = {
            Icon("Edit screenshot", Loc.T("Edit screenshot · Enter / Ctrl+C: copy without editing"), "F0 M2,7 L8,7 L10,4 L16,4 L18,7 L22,7 L22,21 L2,21 Z M4,9 L4,19 L20,19 L20,9 L17,9 L15,6 L11,6 L9,9 Z M12,10 A4,4 0 1 0 12,18 A4,4 0 1 0 12,10 Z M12,12 A2,2 0 1 1 12,16 A2,2 0 1 1 12,12 Z", async () => await edit()),
            Icon("Video", Loc.T("Video"), "F0 M2,5 L16,5 L16,9 L22,6 L22,20 L16,17 L16,21 L2,21 Z M4,7 L4,19 L14,19 L14,7 Z M16,11 L16,15 L20,17 L20,9 Z", selectVideo),
            Icon("Cancel capture", Loc.T("Cancel capture · Esc"), "M5,4 L12,11 L19,4 L21,6 L14,13 L21,20 L19,22 L12,15 L5,22 L3,20 L10,13 L3,6 Z", cancel) } };
        sound=Toggle("Sound",directShow?Loc.T("DirectShow sound"):Loc.T("System sound"),"M3,9 L7,9 L12,4 L12,22 L7,17 L3,17 Z M16,8 L18,6 Q25,13 18,20 L16,18 Q21,13 16,8 Z");
        microphone=Toggle("Microphone",Loc.T("Microphone"),"M9,3 L15,3 L15,14 Q12,18 9,14 Z M5,11 L7,11 L7,15 Q12,22 17,15 L17,11 L19,11 L19,16 Q17,21 13,21 L13,24 L17,24 L17,26 L7,26 L7,24 L11,24 L11,21 Q7,21 5,16 Z");
        cursor=Toggle("Include cursor",Loc.T("Include cursor"),"M5,2 L5,20 L10,15 L14,23 L17,21 L13,13 L20,13 Z");
        SetCursorIncluded(includeCursor);
        cursor.IsCheckedChanged+=(_,_)=>{if(!syncing)cursorChanged?.Invoke(cursor.IsChecked==true);};
        sound.IsEnabled=!animation;ToolTip.SetShowOnDisabled(sound,true);
        if(animation)ToolTip.SetTip(sound,Loc.T("Animations are silent."));
        SetSources(sources??new(true,false));SetMicrophoneAvailable(true);
        sound.IsCheckedChanged+=(_,_)=>{if(!syncing)sourcesChanged?.Invoke(Sources);};
        microphone.IsCheckedChanged+=(_,_)=>{if(!syncing)sourcesChanged?.Invoke(Sources);};
        var start=Icon("Record",Loc.T("Record"),"M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 Z",async()=>{if(record is not null)await record(Sources);});start.IsEnabled=record is not null;
        var countdown=new Border{Width=CapturePanelStyle.ButtonSize,Height=CapturePanelStyle.ButtonSize,CornerRadius=new CornerRadius(6),
            Focusable=true,Child=countdownValue,Background=Brushes.Transparent};
        countdown.Bind(BackgroundProperty,countdown.GetResourceObservable("CaptureButtonBackground"));
        AutomationProperties.SetName(countdown,"Recording countdown");
        ToolTip.SetTip(countdown,Loc.T("Scroll to change the countdown for this recording."));
        SetCountdown(countdownSeconds);
        void AdjustCountdown(int delta)
        {
            SetCountdown(Math.Clamp(this.countdownSeconds+delta,0,10));
            countdownChanged?.Invoke(this.countdownSeconds);
        }
        countdown.PointerWheelChanged+=(_,e)=>
        {
            if(e.Delta.Y==0)return;
            AdjustCountdown(Math.Sign(e.Delta.Y));
            e.Handled=true;
        };
        countdown.KeyDown+=(_,e)=>
        {
            if(e.Key is not (Key.Up or Key.Down))return;
            AdjustCountdown(e.Key==Key.Up?1:-1);
            e.Handled=true;
        };
        video = new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Spacing=4,Children={start,sound,microphone,cursor,countdown,
            Icon("Back",Loc.T("Back"),"M11,3 L13,5 L7,11 L23,11 L23,14 L7,14 L13,20 L11,22 L1,12 Z",back)}};
        var content=Ui.Stack(choosing, MoveGrip, video, info, Status);content.Spacing=6;Child=content;
    }
    public void Update(CaptureEditorState state, CaptureInteractionMode mode)
    {
        IsEnabled = !state.IsExporting;
        choosing.IsVisible = mode == CaptureInteractionMode.Choosing;
        video.IsVisible = mode == CaptureInteractionMode.Video;
        MoveGrip.IsVisible=video.IsVisible;
        info.IsVisible=mode==CaptureInteractionMode.Video;
        Status.IsVisible=!string.IsNullOrWhiteSpace(Status.Text);
        info.Text=Loc.F("{0} × {1} px\nLive region",state.Region.Width,state.Region.Height);
    }
    private static Button Icon(string name,string hint,string data,Action action)
    {
        var button=Ui.Button("",action);button.Content=new PathIcon{Data=Geometry.Parse(data),Width=22,Height=22};
        button.Width=CapturePanelStyle.ButtonSize;button.Height=CapturePanelStyle.ButtonSize;button.Padding=new Thickness(8);
        CapturePanelStyle.Apply(button);AutomationProperties.SetName(button,name);ToolTip.SetTip(button,Loc.T(hint));return button;
    }
    private static ToggleButton Toggle(string name,string hint,string data)
    {
        var button=new ToggleButton{Content=new PathIcon{Data=Geometry.Parse(data),Width=22,Height=22},Width=CapturePanelStyle.ButtonSize,Height=CapturePanelStyle.ButtonSize,Padding=new Thickness(8)};
        CapturePanelStyle.Apply(button);AutomationProperties.SetName(button,name);ToolTip.SetTip(button,Loc.T(hint));return button;
    }
}



