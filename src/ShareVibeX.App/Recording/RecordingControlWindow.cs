using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using ShareVibeX.Platform.Windows;
using System.Globalization;
using PixelRect=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Recording;

public sealed class RecordingControlWindow : Window
{
    private readonly TextBlock heading=Ui.Text("Starting recording…",13);
    private readonly TextBlock metrics=Ui.Text("—",12);
    private readonly TextBlock detail=Ui.Text("",11,true);
    private readonly Button stopButton,cancelButton,closeButton,pauseButton;
    private readonly Border moveGrip;
    private bool canMove;
    private Func<PixelRect>? getRegion;
    private Action<PixelRect,int,int>? moveRegion;
    private readonly ToggleButton microphoneButton;
    private Func<Task>? pauseResume;
    private Func<bool,Task>? setMicrophone;
    public bool IsCaptureExcluded {get;private set;}
    private bool disposed,stopBusy,cancelBusy,pauseBusy,microphoneBusy,syncing,canPause,canMicrophone,microphoneSelected;
    private RecordingSessionState state=RecordingSessionState.Starting;
    public RecordingControlWindow(Func<Task> stop,Func<Task> cancel,Action closeResult,Func<Task>? pauseResume=null,Func<bool,Task>? setMicrophone=null)
    {
        this.pauseResume=pauseResume;this.setMicrophone=setMicrophone;
        Title=Loc.T("ShareVibeX · Recording");Width=224;Height=96;CanResize=false;WindowDecorations=WindowDecorations.None;
        ShowInTaskbar=false;Topmost=true;ShowActivated=false;CapturePanelStyle.Apply(this);
        detail.TextTrimming=TextTrimming.CharacterEllipsis;detail.MaxHeight=14;detail.TextWrapping=TextWrapping.NoWrap;
        ToolTip.SetShouldUseOverlayLayer(detail,true);
        stopButton=CreateIcon("Stop","M4,4 L20,4 L20,20 L4,20 Z",()=>_=ExecuteAsync(false,stop));
        pauseButton=CreateIcon("Pause","M5,4 L10,4 L10,20 L5,20 Z M14,4 L19,4 L19,20 L14,20 Z",()=>_=PauseResumeAsync());
        microphoneButton=new(){Width=CapturePanelStyle.ButtonSize,Height=CapturePanelStyle.ButtonSize,Padding=new Thickness(8),Content=new PathIcon{Data=Geometry.Parse("M9,3 L15,3 L15,14 Q12,18 9,14 Z M5,11 L7,11 L7,15 Q12,22 17,15 L17,11 L19,11 L19,16 Q17,21 13,21 L13,24 L17,24 L17,26 L7,26 L7,24 L11,24 L11,21 Q7,21 5,16 Z"),Width=22,Height=22}};
        CapturePanelStyle.Apply(microphoneButton);AutomationProperties.SetName(microphoneButton,"Microphone");ToolTip.SetTip(microphoneButton,Loc.T("Microphone"));ToolTip.SetShouldUseOverlayLayer(microphoneButton,true);
        microphoneButton.IsCheckedChanged+=(_,_)=>{if(!syncing)_=SwitchMicrophoneAsync(microphoneButton.IsChecked==true);};
        cancelButton=CreateIcon("Cancel recording","M4,6 L6,4 L12,10 L18,4 L20,6 L14,12 L20,18 L18,20 L12,14 L6,20 L4,18 L10,12 Z",()=>_=ExecuteAsync(true,cancel));
        closeButton=CreateIcon("Close result","M4,6 L6,4 L12,10 L18,4 L20,6 L14,12 L20,18 L18,20 L12,14 L6,20 L4,18 L10,12 Z",closeResult);
        moveGrip=new Border{Height=10,Background=Brush.Parse("#805F6670"),CornerRadius=new CornerRadius(4)};
        AutomationProperties.SetName(moveGrip,"Move recording region");
        ToolTip.SetTip(moveGrip,Loc.T("Move recording region"));
        ToolTip.SetShouldUseOverlayLayer(moveGrip,true);
        var hand=new Cursor(StandardCursorType.Hand);
        moveGrip.Cursor=hand;
        Closed+=(_,_)=>hand.Dispose();
        (Avalonia.PixelPoint Point,PixelRect Region)? drag=null;
        moveGrip.AddHandler(PointerPressedEvent,(_,e)=>
        {
            if(!canMove||e.GetCurrentPoint(this).Properties.PointerUpdateKind!=PointerUpdateKind.LeftButtonPressed)return;
            drag=(this.PointToScreen(e.GetPosition(this)),getRegion!());
            e.Pointer.Capture(moveGrip);
            e.Handled=true;
        },RoutingStrategies.Tunnel);
        moveGrip.PointerMoved+=(_,e)=>
        {
            if(!canMove||drag is not {} start)return;
            var point=this.PointToScreen(e.GetPosition(this));
            moveRegion!(start.Region,point.X-start.Point.X,point.Y-start.Point.Y);
        };
        moveGrip.AddHandler(PointerReleasedEvent,(_,e)=>
        {
            if(drag is null||e.InitialPressMouseButton!=MouseButton.Left)return;
            drag=null;e.Pointer.Capture(null);e.Handled=true;
        },RoutingStrategies.Tunnel);
        moveGrip.PointerCaptureLost+=(_,_)=>drag=null;
        var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=2,Children={stopButton,pauseButton,microphoneButton,cancelButton,closeButton}};
        var content=Ui.Stack(moveGrip,row,metrics,detail);content.Spacing=6;metrics.TextWrapping=TextWrapping.NoWrap;metrics.TextTrimming=TextTrimming.CharacterEllipsis;
        var panel=new Border{Child=content};CapturePanelStyle.Apply(panel);Content=panel;
        Closed+=(_,_)=>disposed=true;
        Closing+=(_,e)=>{if(!disposed && IsRecording()){e.Cancel=true;_=ExecuteAsync(true,cancel);}};
        moveGrip.IsVisible=false;UpdateButtons();
    }
    public bool TryPresent(PixelRect region,MonitorInfo monitor,Func<nint,bool>? exclude=null,CapturePanelPlacement? anchor=null,bool roomForMove=false)
    {
        var excluded=(exclude??WindowsCaptureExclusion.TryExclude)(TryGetPlatformHandle()?.Handle??0);
        IsCaptureExcluded=excluded;
        var preferredWidth=anchor?.Width??224;
        var extraHeight=excluded&&roomForMove?16:0;
        Width=Math.Min(preferredWidth,monitor.Bounds.Width/monitor.Scale);Height=Math.Min((anchor?.Height??96)+extraHeight,monitor.Bounds.Height/monitor.Scale);
        var position=RecordingControlPlacement.TryPlace(region,monitor,new(Width,Height),excluded);
        if(anchor is not null&&double.IsFinite(Width)&&Width>0&&double.IsFinite(Height)&&Height>0)
        {
            var bounds=new PixelRect(anchor.Position.X,anchor.Position.Y,(int)Math.Ceiling(Width*monitor.Scale),(int)Math.Ceiling(Height*monitor.Scale));
            if(bounds==bounds.Intersect(monitor.Bounds)&&(excluded||bounds.Intersect(region).IsEmpty))position=anchor.Position;
        }
        if(position is null)return false;
        Position=new(position.Value.X,position.Value.Y);Show();Position=new(position.Value.X,position.Value.Y);return true;
    }
    public void FollowRegion(PixelRect region,MonitorInfo monitor)
    {
        var bounds=monitor.Bounds;
        var width=(int)Math.Ceiling(Width*monitor.Scale);
        var height=(int)Math.Ceiling(Height*monitor.Scale);
        var x=Math.Clamp(region.X+(region.Width-width)/2,bounds.X,Math.Max(bounds.X,bounds.Right-width));
        var y=Math.Clamp(region.Bottom+2,bounds.Y,Math.Max(bounds.Y,bounds.Bottom-height));
        var position=new Avalonia.PixelPoint(x,y);
        if(Position!=position)Position=position;
    }
    public void ConfigureRegionMove(Func<PixelRect> region,Action<PixelRect,int,int> move)
    {
        getRegion=region;moveRegion=move;
    }
    public void SetRegionMoveAvailable(bool available)
    {
        canMove=available;
        moveGrip.IsVisible=available;
    }
    public void ConfigureRecordingActions(Func<Task> pauseResume,Func<bool,Task> setMicrophone)
    {this.pauseResume=pauseResume;this.setMicrophone=setMicrophone;UpdateButtons();}
    public void Update(RecordingSessionState value,RecordingProgress progress,int width,int height,string? path=null,
        bool canPause=false,bool canMicrophone=false,bool microphoneEnabled=false)
    {
        if(disposed)return;state=value;this.canPause=canPause;this.canMicrophone=canMicrophone;microphoneSelected=microphoneEnabled;
        syncing=true;microphoneButton.IsChecked=microphoneSelected;syncing=false;
        heading.Text=value switch{RecordingSessionState.Starting=>Loc.T("Starting recording…"),RecordingSessionState.Finalizing=>Loc.T("Finalizing recording…"),
            RecordingSessionState.Pausing=>Loc.T("Pausing recording…"),RecordingSessionState.Paused=>Loc.T("Recording paused"),RecordingSessionState.Resuming=>Loc.T("Resuming recording…"),
            RecordingSessionState.Result=>Loc.T("Recording retained"),RecordingSessionState.Failed=>Loc.T("Recording failed"),_=>Loc.T("Recording")};
        var duration=progress.Duration is {} time?time.ToString(@"hh\:mm\:ss",CultureInfo.InvariantCulture):"—";
        var resolution=width>0&&height>0?$"{width} × {height} px":"—";
        var bitrate=progress.AverageBitrateKbps is {} rate?rate.ToString("0",CultureInfo.InvariantCulture)+" kbps":"—";
        var size=progress.TotalBytes is {} bytes?(bytes/1048576.0).ToString("0.00",CultureInfo.InvariantCulture)+" MiB":"—";
        var approximation=progress.IsEstimated?"≈ ":"";metrics.Text=$"{duration} · {resolution}\n{approximation}{bitrate} · {approximation}{size}";
        ToolTip.SetTip(metrics,(progress.IsEstimated?Loc.T("Estimated from output dimensions and encoding settings; actual size depends on content.\n"):"")+metrics.Text);
        detail.Text=path??"";detail.IsVisible=false;if(path is not null){metrics.Text=heading.Text+"\n"+path;ToolTip.SetTip(metrics,path);}ToolTip.SetTip(detail,path);UpdateButtons();
    }
    public void Dismiss(){disposed=true;Close();}
    private bool IsRecording()=>state is RecordingSessionState.Starting or RecordingSessionState.Recording or RecordingSessionState.Finalizing
        or RecordingSessionState.Pausing or RecordingSessionState.Paused or RecordingSessionState.Resuming;
    private void UpdateButtons()
    {
        stopButton.IsVisible=IsRecording();cancelButton.IsVisible=IsRecording()||state==RecordingSessionState.Failed;closeButton.IsVisible=!IsRecording();
        stopButton.IsEnabled=!stopBusy && state!=RecordingSessionState.Finalizing;cancelButton.IsEnabled=!cancelBusy;
        pauseButton.IsVisible=IsRecording() && pauseResume is not null;microphoneButton.IsVisible=IsRecording() && setMicrophone is not null;
        pauseButton.IsEnabled=canPause&&!pauseBusy&&!stopBusy;microphoneButton.IsEnabled=canMicrophone&&microphoneAvailable&&!microphoneBusy&&!stopBusy;
        var name=state==RecordingSessionState.Paused?"Resume":"Pause";AutomationProperties.SetName(pauseButton,name);ToolTip.SetTip(pauseButton,Loc.T(name));
        ((PathIcon)pauseButton.Content!).Data=Geometry.Parse(state==RecordingSessionState.Paused?"M6,3 L22,12 L6,21 Z":"M5,4 L10,4 L10,20 L5,20 Z M14,4 L19,4 L19,20 L14,20 Z");
        ToolTip.SetTip(microphoneButton,silentAnimation?Loc.T("Animations are silent."):!microphoneAvailable?Loc.T(MicrophoneAvailability.UnavailableMessage):canMicrophone?Loc.T("Microphone"):Loc.T("Microphone unavailable"));ToolTip.SetShowOnDisabled(microphoneButton,true);
    }
    private bool microphoneAvailable=true;
    private bool silentAnimation;
    public void SetMicrophoneAvailable(bool available,bool animation=false){silentAnimation=animation;microphoneAvailable=available;UpdateButtons();}
    private async Task PauseResumeAsync()
    {
        if(disposed||pauseBusy||!canPause||pauseResume is null)return;pauseBusy=true;UpdateButtons();
        try{await pauseResume();}catch(Exception error){if(!disposed){ReportError(error.Message);}}
        finally{pauseBusy=false;if(!disposed)UpdateButtons();}
    }
    private async Task SwitchMicrophoneAsync(bool enabled)
    {
        if(disposed||microphoneBusy||setMicrophone is null)return;
        if(!canMicrophone||!microphoneAvailable){syncing=true;microphoneButton.IsChecked=microphoneSelected;syncing=false;return;}
        microphoneBusy=true;UpdateButtons();
        try{await setMicrophone(enabled);microphoneSelected=enabled;}
        catch(Exception error){if(!disposed){syncing=true;microphoneButton.IsChecked=microphoneSelected;syncing=false;ReportError(error.Message);}}
        finally{microphoneBusy=false;if(!disposed)UpdateButtons();}
    }
    private async Task ExecuteAsync(bool cancel,Func<Task> action)
    {
        if(disposed || (cancel?cancelBusy:stopBusy))return;
        if(cancel)cancelBusy=true;else stopBusy=true;UpdateButtons();
        try{await action();}catch(Exception error){if(!disposed){ReportError(error.Message);}}
        finally{if(cancel)cancelBusy=false;else stopBusy=false;if(!disposed)UpdateButtons();}
    }
    private void ReportError(string message){detail.Text=message;detail.IsVisible=false;metrics.Text=Loc.T("Recording action failed\n")+message;ToolTip.SetTip(metrics,message);}
    private static Button CreateIcon(string name,string data,Action action)
    {
        var button=Ui.Button("",action);CapturePanelStyle.Apply(button);button.Width=CapturePanelStyle.ButtonSize;button.Height=CapturePanelStyle.ButtonSize;button.Padding=new Thickness(8);
        button.Content=new PathIcon{Data=Geometry.Parse(data),Width=22,Height=22};AutomationProperties.SetName(button,name);ToolTip.SetTip(button,Loc.T(name));
        ToolTip.SetShouldUseOverlayLayer(button,true);return button;
    }
}




