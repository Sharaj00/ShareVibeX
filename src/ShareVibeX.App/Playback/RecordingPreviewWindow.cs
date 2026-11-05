using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ShareVibeX.App.Recording;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Playback;
using Avalonia.Platform.Storage;
using ShareVibeX.App.Export;
using ShareVibeX.App.Capture;
using R=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Playback;

public sealed class RecordingPreviewWindow:Window
{
    private readonly Image image=new(){Stretch=Stretch.Uniform};
    private readonly TextBlock message=Ui.Text("Loading preview…",14);
    private readonly TextBlock time=Ui.Text("—",12);
    private readonly WrapPanel actions=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
    private readonly Button previousFrame,nextFrame;
    private bool timelineHovered,keyboardTimeline;private int outputRate;
    private R region;private MonitorInfo? panelMonitor;
    public Border TimelineOverlay {get;}=new(){IsVisible=false,VerticalAlignment=VerticalAlignment.Bottom,Background=Brushes.Transparent,Padding=new Thickness(8),CornerRadius=new CornerRadius(6)};
    private Button? keepButton;private bool controlHeld;private Button discardButton=null!;
    private readonly Button play;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    private readonly StackPanel stripBody=new(){Spacing=6};
    private readonly List<Button> editButtons=[];
    private readonly TextBlock selectionText=Ui.Text("",12),exportStatus=Ui.Text("",12);
    private readonly StackPanel closePrompt=new(){Spacing=6,IsVisible=false};
    private VideoExportSession? exports;private RecordingWorkspace? workspace;
    private Func<string,CancellationToken,Task>? publish;private Func<Task<string?>>? pickSave;
    private Task actionTask=Task.CompletedTask;private CancellationTokenSource? actionCancellation;
    private Task<bool>? closeRequest;private TaskCompletionSource<ClosePreviewChoice>? closeChoice;
    private bool cleanupOnClose,resourcesStopped;
    public ABRangeStrip Range {get;}=new();
    public bool IsCloseConfirmationVisible=>closePrompt.IsVisible;
    public Action<string,string?>? ExportCompleted {get;set;}
    private void NotifyExport(string title,string? path){try{ExportCompleted?.Invoke(Loc.T(title),path);}catch(Exception error){DiagnosticLog.Write("Completion feedback failed: "+error.Message);}}
    public Func<string,CancellationToken,Task>? ArchiveVideo {get;set;}
    public string SuggestedVideoName {get;set;}="ShareVibeX-"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
    public string? SuggestedFolder {get;set;}
    private WriteableBitmap? bitmap;private Task? closeTask;private bool dismiss,busy;
    public RecordingPlaybackController Playback {get;}
    public Window ControlStrip {get;}
    public RecordingPreviewWindow(IPreviewDecoderFactory decoder,IPreviewAudioOutputFactory audio)
    {
        time.Height=18;AutomationProperties.SetName(time,"Video time");Title=Loc.T("ShareVibeX · Preview");WindowDecorations=WindowDecorations.None;CanResize=false;ShowInTaskbar=false;Topmost=true;Background=Brushes.Black;
        message.HorizontalAlignment=HorizontalAlignment.Center;message.VerticalAlignment=VerticalAlignment.Center;
                TimelineOverlay.Child=Range;time.Foreground=Brushes.White;selectionText.Foreground=Brushes.White;
        Content=new Grid{Children={image,message,TimelineOverlay}};
        PointerEntered+=(_,_)=>SetTimelineHovered(true);PointerExited+=(_,_)=>SetTimelineHovered(false);
        Range.GotFocus+=(_,_)=>UpdateTimelineVisibility();Range.LostFocus+=(_,_)=>UpdateTimelineVisibility();Range.Changed+=()=>{UpdateSelection();UpdateTimelineVisibility();};
        Playback=new(decoder,audio,PresentAsync);Playback.Changed+=PlaybackChanged;
        Range.SeekRequested+=frame=>Observe(RunActionAsync(_=>Playback.SeekAsync(TimeSpan.FromSeconds(frame/(double)Math.Max(1,outputRate)))));
        play=CreateIcon("Play","M6,3 L22,12 L6,21 Z",()=>Observe(ToggleAsync()));
        var close=CreateIcon("Discard video","M5,7 L19,7 L18,21 L6,21 Z M3,3 L21,3 L21,5 L3,5 Z",()=>Observe(DiscardVideoAsync()));discardButton=close;
        previousFrame=CreateIcon("Previous frame","M16,4 L16,20 L20,20 L20,4 Z M14,5 L2,12 L14,19 Z",()=>Observe(StepFrameAsync(-1)));
        nextFrame=CreateIcon("Next frame","M4,4 L4,20 L8,20 L8,4 Z M10,5 L22,12 L10,19 Z",()=>Observe(StepFrameAsync(1)));
        actions.Children.Add(previousFrame);actions.Children.Add(play);actions.Children.Add(nextFrame);actions.Children.Add(close);
        ControlStrip=new Window{Title=Loc.T("ShareVibeX · Playback"),WindowDecorations=WindowDecorations.None,CanResize=false,Width=470,Height=90,ShowInTaskbar=false,Topmost=true};
        CapturePanelStyle.Apply(ControlStrip);stripBody.Children.Add(actions);stripBody.Children.Add(time);
        var panel=new Border{Child=new ScrollViewer{Content=stripBody}};CapturePanelStyle.Apply(panel);ControlStrip.Content=panel;        timer.Tick+=(_,_)=>Refresh();
        Closing+=OnClosing;ControlStrip.Closing+=OnClosing;        void Modifier(bool held)=>SetControlModifier(held);
        void Keys(object? sender,KeyEventArgs e){Modifier(e.KeyModifiers.HasFlag(KeyModifiers.Control));if(e.Key==Key.Z&&controlHeld){actionCancellation?.Cancel();e.Handled=true;}}
        KeyDown+=Keys;ControlStrip.KeyDown+=Keys;KeyUp+=(_,e)=>Modifier(e.KeyModifiers.HasFlag(KeyModifiers.Control));ControlStrip.KeyUp+=(_,e)=>Modifier(e.KeyModifiers.HasFlag(KeyModifiers.Control));Deactivated+=(_,_)=>Modifier(false);ControlStrip.Deactivated+=(_,_)=>Modifier(false);
        KeyDown+=(_,e)=>{if(e.Key==Key.Tab){keyboardTimeline=true;TimelineOverlay.IsVisible=true;Range.Focus();e.Handled=true;}else if(e.Key==Key.Escape){Observe(RequestCloseAsync());e.Handled=true;}else if(e.Key==Key.Space){Observe(ToggleAsync());e.Handled=true;}};
    }
    public async Task OpenAsync(PreviewMedia media,R region,IReadOnlyList<MonitorInfo> monitors,CancellationToken token)
    {
        this.region=region;outputRate=media.FrameRate;Range.Reset(Math.Max(1,(long)Math.Round((media.Duration?.TotalSeconds??0)*outputRate)));
        var placement=PreviewWindowPlacement.ForRegion(region,monitors);panelMonitor=placement.Monitor;Position=placement.Position;Width=placement.Size.Width;Height=placement.Size.Height;
        Opened+=(_,_)=>{Width=region.Width/RenderScaling;Height=region.Height/RenderScaling;Position=placement.Position;};
        ControlStrip.Width=Math.Min(12*(CapturePanelStyle.ButtonSize+4)+16,placement.Monitor.Bounds.Width/placement.Monitor.Scale);
        LayoutControls();        Show();ControlStrip.Show();timer.Start();
        try
        {
            token.ThrowIfCancellationRequested();
            await Playback.LoadAsync(media,PreviewFormat.Fit(region.Width,region.Height,media.FrameRate),token);
            token.ThrowIfCancellationRequested();
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){await ClosePreviewAsync();throw;}
        catch(Exception error){if(!dismiss){message.Text=error.Message;message.IsVisible=true;}}
        Refresh();
    }
    private async Task PresentAsync(PreviewFrame frame,long generation)
    {
        await Dispatcher.UIThread.InvokeAsync(()=>
        {
            if(dismiss||closeTask is not null||generation!=Playback.Generation)return;
            var format=frame.Format;
            if(bitmap is null||bitmap.PixelSize.Width!=format.Width||bitmap.PixelSize.Height!=format.Height)
            {
                image.Source=null;bitmap?.Dispose();bitmap=new(new(format.Width,format.Height),new(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque);image.Source=bitmap;
            }
            using(var pixels=bitmap.Lock())
            {
                unsafe
                {
                    fixed(byte* source=frame.Pixels.Span)
                        for(var row=0;row<format.Height;row++)Buffer.MemoryCopy(source+row*format.Width*4,(byte*)pixels.Address+row*pixels.RowBytes,pixels.RowBytes,format.Width*4);
                }
            }
            image.InvalidateVisual();message.IsVisible=false;Refresh();
        });
    }
    private void PlaybackChanged(){if(Dispatcher.UIThread.CheckAccess())Refresh();else Dispatcher.UIThread.Post(Refresh);}
    private void Refresh()
    {
        if(dismiss)return;var playing=Playback.State==PreviewPlaybackState.Playing;
        AutomationProperties.SetName(play,playing?"Pause playback":"Play");ToolTip.SetTip(play,playing?Loc.T("Pause"):Loc.T("Play"));
        ((PathIcon)play.Content!).Data=Geometry.Parse(playing?"M5,4 L10,4 L10,20 L5,20 Z M14,4 L19,4 L19,20 L14,20 Z":"M6,3 L22,12 L6,21 Z");
        var editing=busy||exports?.IsBusy==true||closeRequest is not null||resourcesStopped;
        foreach(var button in editButtons)button.IsEnabled=!editing;Range.IsEnabled=!editing;
        if(exports is not null)exportStatus.Text=editing?(exports.Progress is {} progress?Loc.F("Working… {0:P0}",progress):Loc.T("Working…")):exports.Warning??(exports.HasUnexportedChanges?Loc.T("Not exported"):Loc.T("Exported"));
        play.IsEnabled=!editing&&Playback.State is not(PreviewPlaybackState.Loading or PreviewPlaybackState.Seeking or PreviewPlaybackState.Closed);
        var duration=Playback.Duration;Range.IsEnabled=!editing&&duration>TimeSpan.Zero&&Playback.State is not(PreviewPlaybackState.Loading or PreviewPlaybackState.Closed);
        previousFrame.IsEnabled=nextFrame.IsEnabled=Range.IsEnabled;
        Range.SetPosition((long)Math.Round(Playback.Position.TotalSeconds*Math.Max(1,outputRate)));
        time.Text=Playback.Position.ToString(@"hh\:mm\:ss\.fff")+" / "+(duration?.ToString(@"hh\:mm\:ss\.fff")??"—");
        if(Playback.Error is {} error){message.Text=error;message.IsVisible=true;ToolTip.SetTip(message,error);}
    }
    public void SetTimingsVisible(bool visible){time.IsVisible=visible;LayoutControls();}
    public void ConfigureExport(VideoExportSession session,RecordingWorkspace ownedWorkspace,Func<string,CancellationToken,Task> publisher,Func<Task<string?>>? savePicker=null)
    {
        if(exports is not null)throw new InvalidOperationException(Loc.T("Exports are already configured."));
        exports=session;workspace=ownedWorkspace;publish=publisher;pickSave=savePicker??PickSaveAsync;
        Range.Reset(session.Timeline.FrameCount);

        void Add(string name,string geometry,Func<Task> action){var button=CreateIcon(name,geometry,()=>Observe(action()));editButtons.Add(button);actions.Children.Add(button);}
        Add("Set A","M15,3 L7,3 L7,21 L15,21 L15,19 L9,19 L9,5 L15,5 Z",()=>{Range.SetRange(Math.Min(session.Timeline.Snap(Playback.Position),Range.B),Range.B);return Task.CompletedTask;});
        Add("Set B","M9,3 L17,3 L17,21 L9,21 L9,19 L15,19 L15,5 L9,5 Z",()=>{Range.SetRange(Range.A,Math.Max(session.Timeline.Snap(Playback.Position),Range.A));return Task.CompletedTask;});
        Add("Keep A–B","M3,3 L7,3 L7,5 L5,5 L5,19 L7,19 L7,21 L3,21 Z M21,3 L17,3 L17,5 L19,5 L19,19 L17,19 L17,21 L21,21 Z M8,11 L16,11 L16,13 L8,13 Z",()=>ApplyEditAsync(!controlHeld));keepButton=(Button)actions.Children[^1];keepButton.AddHandler(PointerPressedEvent,(_,e)=>SetControlModifier(e.KeyModifiers.HasFlag(KeyModifiers.Control)),RoutingStrategies.Tunnel);
        Add("Copy video","M7,2 L20,2 L20,17 L17,17 L17,5 L7,5 Z M3,7 L15,7 L15,22 L3,22 Z",CopyVideoAsync);
        Add("Save video","M3,3 L18,3 L21,6 L21,21 L3,21 Z M7,4 L7,10 L17,10 L17,4 Z M7,14 L7,20 L17,20 L17,14 Z",SaveVideoAsync);
        actions.Children.Remove(discardButton);actions.Children.Add(discardButton);
        stripBody.Children.Add(exportStatus);
        closePrompt.Children.Add(Ui.Text("Save this video before closing?",13));var choices=new WrapPanel();
        choices.Children.Add(CreateIcon("Save and close","M3,3 L18,3 L21,6 L21,21 L3,21 Z M7,4 L7,10 L17,10 L17,4 Z M7,14 L7,20 L17,20 L17,14 Z",()=>ResolveClose(ClosePreviewChoice.Save)));
        choices.Children.Add(CreateIcon("Discard recording","M5,7 L19,7 L18,21 L6,21 Z M3,3 L21,3 L21,5 L3,5 Z",()=>ResolveClose(ClosePreviewChoice.Discard)));
        choices.Children.Add(CreateIcon("Continue editing","M10,3 L3,10 L10,17 L10,12 C18,12 20,16 20,20 L22,20 C22,12 17,8 10,8 Z",()=>ResolveClose(ClosePreviewChoice.Continue)));
        closePrompt.Children.Add(choices);stripBody.Children.Add(closePrompt);UpdateSelection();
    }
    private void UpdateSelection()
    {
        if(exports is null)return;selectionText.Text=$"A {TimeSpan.FromSeconds(Range.A/(double)exports.Timeline.FrameRate):hh\\:mm\\:ss\\.fff}  ·  B {TimeSpan.FromSeconds(Range.B/(double)exports.Timeline.FrameRate):hh\\:mm\\:ss\\.fff}";
    }
    public Task ApplyEditAsync(bool keep)=>RunActionAsync(async token=>
    {
        var session=exports??throw new InvalidOperationException(Loc.T("Editing is unavailable."));var prior=session.Timeline;await Playback.PauseAsync();
        try{await session.ApplyAsync(Range.A,Range.B,keep,token);await ReloadEditedAsync(token);}
        catch(OperationCanceledException)when(token.IsCancellationRequested){await session.SelectRevisionAsync(prior);await ReloadEditedAsync(CancellationToken.None);throw;}
    });
    public Task ResetEditsAsync()=>RunActionAsync(async token=>
    {
        var prior=exports!.Timeline;await Playback.PauseAsync();
        try{await exports.ResetAsync(token);await ReloadEditedAsync(token);}
        catch(OperationCanceledException)when(token.IsCancellationRequested){await exports.SelectRevisionAsync(prior);await ReloadEditedAsync(CancellationToken.None);throw;}
    });
    private async Task ReloadEditedAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var media=exports!.CurrentMedia;
        await Playback.LoadAsync(media,PreviewFormat.Fit(media.Width,media.Height,media.FrameRate),token);Range.Reset(exports.Timeline.FrameCount);outputRate=media.FrameRate;
    }
    public async Task CopyVideoAsync()
    {
        string? copied=null;
        await RunActionAsync(async token=>
        {
            await Playback.PauseAsync();
            await exports!.CopyAsync(async(path,ct)=>
            {
                await publish!(path,ct);
                if(ArchiveVideo is not null)await ArchiveVideo(path,ct);
                copied=path;
            },token);
        });
        if(copied is not null)await FinishExportAsync("Video copied",copied);
    }
    public Task SaveVideoAsync()=>SaveVideoCoreAsync(false);
    private async Task SaveVideoCoreAsync(bool closing)
    {
        string? saved=null;
        await RunActionAsync(async token=>
        {
            await Playback.PauseAsync();
            var path=await pickSave!();
            token.ThrowIfCancellationRequested();
            if(path is null)return;
            await exports!.SaveAsync(path,token);
            await publish!(path,token);
            if(ArchiveVideo is not null)await ArchiveVideo(path,token);
            saved=path;
        },closing);
        if(saved is not null)await FinishExportAsync("Video saved",saved);
    }
    private Task FinishExportAsync(string title,string path)
    {
        NotifyExport(title,path);
        cleanupOnClose=true;
        return ClosePreviewAsync();
    }
    private async Task<string?> PickSaveAsync()
    {
        IStorageFolder? folder=null;
        if(SuggestedFolder is {} path)
        {
            Directory.CreateDirectory(path);
            folder=await ControlStrip.StorageProvider.TryGetFolderFromPathAsync(path);
        }
        var file=await ControlStrip.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title=Loc.T("Save video"),SuggestedFileName=SuggestedVideoName,DefaultExtension=Path.GetExtension(exports!.CurrentMedia.Path).TrimStart('.'),
            SuggestedStartLocation=folder,ShowOverwritePrompt=true,FileTypeChoices=exports!.SupportedContainers.Select(c=>new FilePickerFileType(c.ToString()){Patterns=["*."+new ShareVibeX.Core.Recording.RecordingOptions{Container=c}.Extension]}).ToArray()
        });return file?.TryGetLocalPath();
    }
    private Task RunActionAsync(Func<CancellationToken,Task> action,bool closing=false)
    {
        if(busy||dismiss||closeTask is not null||closeChoice is not null||resourcesStopped||closeRequest is not null&&!closing)return Task.CompletedTask;
        busy=true;actionCancellation=new();actionTask=RunCoreAsync(action,actionCancellation);Refresh();return actionTask;
    }
    private async Task RunCoreAsync(Func<CancellationToken,Task> action,CancellationTokenSource cancellation)
    {
        await Task.Yield();try{await action(cancellation.Token);}
        finally{busy=false;actionCancellation=null;cancellation.Dispose();Refresh();}
    }
    private async void Observe(Task task)
    {try{await task;}catch(OperationCanceledException){}catch(Exception error){if(!dismiss){message.Text=error.Message;message.IsVisible=true;exportStatus.Text=error.Message;}}}
    private Task ToggleAsync()=>RunActionAsync(async _=>{if(Playback.State==PreviewPlaybackState.Playing)await Playback.PauseAsync();else await Playback.PlayAsync();});
    private void SetControlModifier(bool held)
    {
        controlHeld=held;if(keepButton is null)return;
        AutomationProperties.SetName(keepButton,held?"Delete A–B":"Keep A–B");ToolTip.SetTip(keepButton,held?Loc.T("Delete A–B"):Loc.T("Keep A–B · Ctrl: delete"));
        ((PathIcon)keepButton.Content!).Data=Geometry.Parse(held?"M4,4 L6,4 L12,10 L18,4 L20,4 L14,12 L20,20 L18,20 L12,14 L6,20 L4,20 L10,12 Z":"M3,3 L7,3 L7,5 L5,5 L5,19 L7,19 L7,21 L3,21 Z M21,3 L17,3 L17,5 L19,5 L19,19 L17,19 L17,21 L21,21 Z M8,11 L16,11 L16,13 L8,13 Z");
    }
    public Task StepFrameAsync(int direction)=>RunActionAsync(_=>Playback.StepFrameAsync(direction));
    public void SetTimelineHovered(bool hovered){timelineHovered=hovered;if(hovered)keyboardTimeline=false;UpdateTimelineVisibility();}
    private void UpdateTimelineVisibility()=>TimelineOverlay.IsVisible=timelineHovered||Range.IsDragging||keyboardTimeline&&Range.IsKeyboardFocusWithin;
    private void LayoutControls()
    {
        if(panelMonitor is not {} monitor)return;
        stripBody.Measure(new Size(Math.Max(1,ControlStrip.Width-16),double.PositiveInfinity));
        ControlStrip.Height=Math.Min(monitor.Bounds.Height/monitor.Scale,Math.Max(56,Math.Ceiling(stripBody.DesiredSize.Height+16)));
        var position=OverlayToolbarLayout.Place(new Rect((region.X-monitor.Bounds.X)/monitor.Scale+(region.Width/monitor.Scale-ControlStrip.Width)/2,(region.Y-monitor.Bounds.Y)/monitor.Scale,region.Width/monitor.Scale,region.Height/monitor.Scale),new Size(ControlStrip.Width,Math.Min(monitor.Bounds.Height/monitor.Scale,Math.Max(56,Math.Ceiling(stripBody.DesiredSize.Height+16)+(time.IsVisible?0:24)))),new Size(monitor.Bounds.Width/monitor.Scale,monitor.Bounds.Height/monitor.Scale));
        ControlStrip.Position=new(monitor.Bounds.X+(int)Math.Round(position.X*monitor.Scale),monitor.Bounds.Y+(int)Math.Round(position.Y*monitor.Scale));
    }
    private void OnClosing(object? sender,WindowClosingEventArgs args){if(!dismiss){args.Cancel=true;Observe(RequestCloseAsync());}}
    public async Task DiscardVideoAsync(){actionCancellation?.Cancel();try{await actionTask;}catch{}cleanupOnClose=true;await ClosePreviewAsync();}
    public void ResolveClose(ClosePreviewChoice choice)=>closeChoice?.TrySetResult(choice);
    public Task<bool> RequestCloseAsync()=>closeRequest??=RequestCloseCoreAsync();
    private async Task<bool> RequestCloseCoreAsync()
    {
        await Task.Yield();
        try
        {
            if(dismiss)return true;actionCancellation?.Cancel();try{await actionTask;}catch{ }
            if(resourcesStopped){await ClosePreviewAsync();return true;}
            await Playback.PauseAsync();
            if(exports?.HasUnexportedChanges==true)
            {
                closeChoice=new(TaskCreationOptions.RunContinuationsAsynchronously);closePrompt.IsVisible=true;LayoutControls();Refresh();
                var choice=await closeChoice.Task;closeChoice=null;closePrompt.IsVisible=false;LayoutControls();Refresh();
                if(choice==ClosePreviewChoice.Continue)return false;
                if(choice==ClosePreviewChoice.Save){await SaveVideoCoreAsync(true);if(exports.HasUnexportedChanges)return false;}
            }
            cleanupOnClose=true;await ClosePreviewAsync();return true;
        }
        catch(Exception error){message.Text=error.Message;message.IsVisible=true;return false;}
        finally{closeRequest=null;Refresh();}
    }
    public Task ClosePreviewAsync()=>closeTask??=CloseCoreAsync();
    private async Task CloseCoreAsync()
    {
        await Task.Yield();timer.Stop();actionCancellation?.Cancel();closeChoice?.TrySetResult(ClosePreviewChoice.Continue);
        try
        {
            try{await actionTask;}catch{ }
            if(exports is not null)await exports.DisposeAsync();await Playback.DisposeAsync();resourcesStopped=true;
            if(cleanupOnClose&&workspace is not null)await workspace.DiscardAsync();
        }
        catch
        {
            if(cleanupOnClose){closeTask=null;timer.Start();throw;}throw;
        }
        finally
        {
            if(!cleanupOnClose||closeTask is not null)
            {dismiss=true;Playback.Changed-=PlaybackChanged;image.Source=null;bitmap?.Dispose();bitmap=null;ControlStrip.Close();Close();}
        }
    }
    private static Button CreateIcon(string name,string path,Action action)
    {
        var button=Ui.Button("",action);CapturePanelStyle.Apply(button);button.Width=CapturePanelStyle.ButtonSize;button.Height=CapturePanelStyle.ButtonSize;button.Padding=new Thickness(8);button.Margin=new Thickness(2,0);button.Content=new PathIcon{Data=Geometry.Parse(path),Width=22,Height=22};
        AutomationProperties.SetName(button,name);ToolTip.SetTip(button,Loc.T(name));ToolTip.SetShouldUseOverlayLayer(button,true);return button;
    }
}
public enum ClosePreviewChoice {Save,Discard,Continue}











