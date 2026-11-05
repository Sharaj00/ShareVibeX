using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Recording;
using R=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Capture;

public sealed class SelectionOverlayController(IDesktopCapture capture,ClipboardImageOwner clipboard,AppSettings settings,
    Func<TopLevel,CapturedFrame,Task>? publish = null, Func<CancellationToken,Task>? waitForDesktop = null, Func<Window>? windowFactory = null,
    Func<RecordingStartRequest,CancellationToken,Task>? record = null, ShareVibeX.App.History.CaptureHistory? history=null, ShareVibeX.App.History.CaptureHistoryContext? historyContext=null, Action<bool,string?>? completed=null, Action<CapturedFrame>? previewReady=null,Func<ShareVibeX.Core.Capture.PixelPoint>? cursorPosition=null,Func<R,IDisposable>? constrainCursor=null,Func<ShareVibeX.Core.Capture.PixelPoint,bool,R?>? detectRegion=null)
{
    public Func<RecordingOptions,CancellationToken,Task<bool>>? MicrophoneProbe {get;init;}
    public bool ShowActivated {get;init;}=true;
    public async Task<CaptureSession?> SelectAsync(DesktopLayout layout,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var selectionOptions=settings.SelectionDefaults;
        R? activeMonitor=null;
        if(selectionOptions.RestrictToActiveMonitor)
        {
            var cursor=(cursorPosition??ShareVibeX.Platform.Windows.WindowsSelectionTools.GetCursorPosition)();
            activeMonitor=(layout.Monitors.FirstOrDefault(m=>cursor.X>=m.Bounds.X&&cursor.X<m.Bounds.Right&&cursor.Y>=m.Bounds.Y&&cursor.Y<m.Bounds.Bottom)??layout.Monitors[0]).Bounds;
        }
        using var cursorConstraint=activeMonitor is {} active?(constrainCursor??ShareVibeX.Platform.Windows.WindowsSelectionTools.ConstrainCursor)(active):null;
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);token=lifetime.Token;
        var completion=new TaskCompletionSource<CaptureSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state=new CaptureEditorState();
        DesktopSnapshot snapshot = null!;
        var hosts=new List<(Window Window,EditorSurface Surface,Canvas Canvas)>();
        var toolbars=new List<EditorToolbar>();
        var modebars=new List<CaptureModeToolbar>();
        var frameCapture=new OverlayFrameCapture(capture,()=>{foreach(var host in hosts)host.Window.Hide();},()=>
        {
            if(completion.Task.IsCompleted || token.IsCancellationRequested)return;
            foreach(var host in hosts)host.Window.Show();
        },waitForDesktop??WaitForDesktop);
        var modes=new CaptureModeSession(layout,frameCapture.TakeAsync);
        Bitmap? preview=null;
        CapturedFrame? rendererFrame=null;
        var busy=false;
        var recordingStarted=false;
        var countdownSeconds=settings.RecordingCountdownSeconds;
        var pickingColor=false;
        var includeCursor=settings.RecordingDefaults.IncludeCursor;
        var videoSources=RecordingSourceChoices.FromOptions(settings.RecordingDefaults);
        var checkingMicrophone=false;var nextMicrophoneCheck=DateTime.MinValue;
        async Task CheckMicrophone()
        {
            if(settings.RecordingDefaults.IsAnimation||MicrophoneProbe is null||checkingMicrophone)return;checkingMicrophone=true;
            try
            {
                var available=await MicrophoneProbe(settings.RecordingDefaults,token);
                if(token.IsCancellationRequested||completion.Task.IsCompleted)return;
                if(!available)videoSources=videoSources with{Microphone=false};
                foreach(var bar in modebars){bar.SetMicrophoneAvailable(available);bar.SetSources(videoSources);}
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
            finally{checkingMicrophone=false;nextMicrophoneCheck=DateTime.UtcNow.AddSeconds(2);}
        }
        InlineTextEditor? textEditor=null;
        Canvas? textCanvas=null;
        var lastRender=System.Diagnostics.Stopwatch.GetTimestamp();
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(400)};
        timer.Tick+=(_,_)=>
        {
            if(modes.Mode==CaptureInteractionMode.Video&&DateTime.UtcNow>=nextMicrophoneCheck)_=CheckMicrophone();
            try {if(!capture.GetMonitors().SequenceEqual(layout.Monitors))completion.TrySetException(new InvalidOperationException(Loc.T("The display layout changed. Start a new capture.")));}
            catch(Exception error){completion.TrySetException(error);}
        };
        using var cancellation=token.Register(()=>Dispatcher.UIThread.Post(()=>completion.TrySetCanceled(token)));
        try
        {
            foreach(var monitor in layout.Monitors)
            {
                var window=windowFactory?.Invoke()??new Window();
                window.Title=Loc.T("ShareVibeX · Capture");window.WindowDecorations=WindowDecorations.None;window.CanResize=false;
                window.ShowInTaskbar=false;window.ShowActivated=ShowActivated;window.Topmost=true;window.Position=new(monitor.Bounds.X,monitor.Bounds.Y);
                window.Width=monitor.Bounds.Width/monitor.Scale;window.Height=monitor.Bounds.Height/monitor.Scale;
                window.Background=Brushes.Transparent;window.TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
                window.TransparencyBackgroundFallback=Brushes.Transparent;
                var surface=new EditorSurface(state,layout,monitor) {Options=selectionOptions,ActiveMonitorBounds=activeMonitor,DetectRegion=selectionOptions.DetectWindows?(detectRegion??ShareVibeX.Platform.Windows.WindowsSelectionTools.FindRegion):null,IsLiveSelection=true,PickColor=PickColor,Cancel=()=>{if(busy)return;if(textEditor is not null)FinishText(false);else completion.TrySetResult(null);}};
                var canvas=new Canvas();
                var toolbar=new EditorToolbar(state,()=>Refresh(true),save=>Export(window,save),()=>{if(!busy)completion.TrySetResult(null);},()=>SetColorPicking(!pickingColor)) {IsVisible=false};
                toolbars.Add(toolbar);canvas.Children.Add(toolbar);
                var modebar=new CaptureModeToolbar(()=>EnterEditor(window),
                    ()=>{if(busy || state.IsDrawing)return;modes.SelectVideo();Refresh(false);_=CheckMicrophone();},()=>{if(busy || state.IsDrawing)return;modes.BackToChoosing();Refresh(false);},()=>{if(!busy)completion.TrySetResult(null);},
                    record is null?null:sources=>StartRecording(sources),videoSources,sources=>
                    {videoSources=sources;foreach(var bar in modebars)bar.SetSources(sources);},settings.RecordingDefaults.AudioSource==AudioSource.DirectShow,settings.RecordingDefaults.IsAnimation,includeCursor,
                    included=>{includeCursor=included;foreach(var bar in modebars)bar.SetCursorIncluded(included);},countdownSeconds,seconds=>
                    {countdownSeconds=seconds;foreach(var bar in modebars)bar.SetCountdown(seconds);}){IsVisible=false};
                modebar.MoveGrip.Configure(()=>state.Region,(region,dx,dy)=>
                {
                    var bounds=activeMonitor??layout.Bounds;
                    if(monitor.Bounds.Intersect(region)==region)bounds=monitor.Bounds;
                    state.Region=SelectionRules.Move(region,dx,dy,bounds);
                    Refresh(false);
                });
                modebar.MoveGrip.SetAvailable(true);
                modebars.Add(modebar);canvas.Children.Add(modebar);
                modebar.SizeChanged+=(_,args)=>
                {
                    if(modebar.IsVisible)PlaceToolbar(window,monitor,modebar,args.NewSize);
                };
                window.Content=new Grid {Children={surface,canvas}};
                hosts.Add((window,surface,canvas));
                var index=hosts.Count-1;
                surface.Changed=Refresh;
                surface.BeforePointerDown=()=>FinishText(true);
                surface.TextEditRequested=annotation=>BeginText(index,annotation);
                window.Opened+=(_,_)=>{window.Width=monitor.Bounds.Width/window.RenderScaling;window.Height=monitor.Bounds.Height/window.RenderScaling;window.Position=new(monitor.Bounds.X,monitor.Bounds.Y);if(ShowActivated)surface.Focus();};
                window.Closed+=(_,_)=>{modebar.MoveGrip.Dispose();completion.TrySetResult(null);};
                window.AddHandler(InputElement.KeyDownEvent,(_,e)=>
                {
                    if(pickingColor&&e.Key==Key.Escape){SetColorPicking(false);e.Handled=true;return;}
                    if(modes.Mode!=CaptureInteractionMode.Choosing || busy || e.Source is TextBox)return;
                    if(e.Key==Key.Enter || e.Key==Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    {_=Export(window,false);e.Handled=true;}
                },RoutingStrategies.Tunnel);
                window.KeyDown+=(_,e)=>
                {
                    if(busy)return;
                    if(e.Key is Key.LeftCtrl or Key.RightCtrl && window.FocusManager?.GetFocusedElement() is not TextBox)
                    {state.MoveModifier=true;foreach(var host in hosts)host.Surface.InvalidateVisual();return;}
                    if(e.Key==Key.Escape && textEditor is not null){FinishText(false);surface.Focus();e.Handled=true;return;}
                    if(e.Key==Key.Escape && window.FocusManager?.GetFocusedElement() is TextBox){surface.Focus();e.Handled=true;return;}
                    if(e.Key==Key.Escape){completion.TrySetResult(null);e.Handled=true;return;}
                    if(e.Source is TextBox || window.FocusManager?.GetFocusedElement() is TextBox)return;
                    var ctrl=e.KeyModifiers.HasFlag(KeyModifiers.Control);
                    if(e.Key==Key.Enter || ctrl&&e.Key==Key.C){if(modes.Mode!=CaptureInteractionMode.Video)_=Export(window,false);e.Handled=true;return;}
                    if(modes.Mode!=CaptureInteractionMode.EditingScreenshot)return;
                    if(ctrl&&e.Key==Key.S){_=Export(window,true);e.Handled=true;return;}
                    if(EditorKeyboard.HandleHistory(state,e.Key,e.KeyModifiers)) { }
                    else if(e.Key==Key.Delete){if(state.SelectedId is {} id)state.Document.Delete(id);state.SelectedId=null;}
                    else if(!ctrl)
                    {
                        var tool=e.Key switch {Key.Tab=>EditorTool.Region,Key.R=>EditorTool.Rectangle,Key.E=>EditorTool.Ellipse,Key.F=>EditorTool.Pen,Key.L=>EditorTool.Line,Key.A=>EditorTool.Arrow,Key.T=>EditorTool.Text,_=>(EditorTool?)null};
                        if(tool is null)return;state.Tool=tool.Value;
                    }
                    else return;
                    Refresh(true);e.Handled=true;
                };
                window.KeyUp+=(_,e)=>{if(e.Key is Key.LeftCtrl or Key.RightCtrl){state.MoveModifier=e.KeyModifiers.HasFlag(KeyModifiers.Control);foreach(var host in hosts)host.Surface.InvalidateVisual();}};
                window.Deactivated+=(_,_)=>{state.MoveModifier=false;foreach(var host in hosts)host.Surface.InvalidateVisual();};
                window.Show();
            }
            timer.Start();return await completion.Task;
        }
        finally {lifetime.Cancel();timer.Stop();FinishText(false);foreach(var host in hosts)host.Window.Close();preview?.Dispose();}

        void BeginText(int index,Annotation annotation)
        {
            FinishText(true);state.LoadStyle(annotation);state.SelectedId=annotation.Id;state.IsEditingText=true;state.EditingTextId=annotation.Id;
            var host=hosts[index];var monitor=layout.Monitors[index];InlineTextEditor? created=null;
            created=new InlineTextEditor(annotation,monitor.Bounds,host.Window.RenderScaling,
                result=>CompleteText(created!,result),()=>CompleteText(created!,null),layout.Bounds);
            textEditor=created;textCanvas=host.Canvas;host.Canvas.Children.Add(created);Refresh(true);
            Dispatcher.UIThread.Post(()=>{if(ReferenceEquals(textEditor,created))created.Input.Focus();},DispatcherPriority.Background);
        }
        void CompleteText(InlineTextEditor editor,Annotation? result)
        {
            if(!ReferenceEquals(editor,textEditor))return;
            textEditor=null;var canvas=textCanvas;textCanvas=null;state.IsEditingText=false;state.EditingTextId=null;canvas?.Children.Remove(editor);
            if(result is not null)
            {
                var exists=state.Document.Items.Any(item=>item.Id==result.Id);
                if(string.IsNullOrWhiteSpace(result.Text)){if(exists)state.Document.Delete(result.Id);state.SelectedId=null;}
                else{if(exists)state.Document.Replace(result);else state.Document.Add(result);state.SelectedId=result.Id;}
            }
            Refresh(true);
        }
        void FinishText(bool commit){var editor=textEditor;if(editor is null)return;if(commit)editor.Commit();else editor.Cancel();}

        void Refresh(bool force)
        {
            if(completion.Task.IsCompleted)return;
            textEditor?.SetColor(state.Color);
            // Region-only drags reuse the bitmap; annotation drags are limited to about 30 previews/sec.
            var render=modes.Mode==CaptureInteractionMode.EditingScreenshot && (force || state.Draft is not null && System.Diagnostics.Stopwatch.GetElapsedTime(lastRender).TotalMilliseconds>=33);
            if(render)
            {
                var items=state.PreviewItems();
                rendererFrame=items.Count==0?snapshot.Frame:AnnotationRenderer.Render(snapshot,items);
                var next=BitmapExportService.ToBitmap(rendererFrame);var old=preview;preview=next;
                foreach(var host in hosts)host.Surface.Image=next;
                old?.Dispose();lastRender=System.Diagnostics.Stopwatch.GetTimestamp();
            }
            var best=layout.Monitors.Select((monitor,index)=>(Index:index,Area:(long)monitor.Bounds.Intersect(state.Region).Width*monitor.Bounds.Intersect(state.Region).Height))
                .OrderByDescending(item=>item.Area).First().Index;
            for(var i=0;i<hosts.Count;i++)
            {
                var host=hosts[i];var editing=modes.Mode==CaptureInteractionMode.EditingScreenshot;
                host.Surface.IsLiveSelection=!editing;
                toolbars[i].Update(state);modebars[i].Update(state,modes.Mode);
                toolbars[i].IsVisible=editing&&!state.Region.IsEmpty&&i==best;
                modebars[i].IsVisible=!editing&&!state.IsDrawing&&!state.Region.IsEmpty&&i==best;
                Control toolbar=editing?toolbars[i]:modebars[i];
                var monitor=layout.Monitors[i];var scale=host.Window.RenderScaling;
                var availableWidth=Math.Max(1,host.Window.Width-24);
                toolbar.MaxWidth=availableWidth;
                toolbar.Width=editing?OverlayToolbarLayout.EditorWidth(state.Region.Width,scale,host.Window.Width):double.NaN;
                toolbar.Measure(new Size(availableWidth,double.PositiveInfinity));
                var toolbarWidth=double.IsNaN(toolbar.Width)?toolbar.DesiredSize.Width:toolbar.Width;
                var height=Math.Max(1,toolbar.DesiredSize.Height);
                PlaceToolbar(host.Window,monitor,toolbar,new Size(toolbarWidth,height));
                host.Surface.InvalidateVisual();
            }
        }
        void PlaceToolbar(Window window,MonitorInfo monitor,Control toolbar,Size size)
        {
            var scale=window.RenderScaling;
            var region=new Rect((state.Region.X-monitor.Bounds.X)/scale,(state.Region.Y-monitor.Bounds.Y)/scale,state.Region.Width/scale,state.Region.Height/scale);
            var position=OverlayToolbarLayout.Place(region,size,new Size(window.Width,window.Height));
            Canvas.SetLeft(toolbar,position.X);Canvas.SetTop(toolbar,position.Y);
        }
        void SetColorPicking(bool enabled)
        {
            pickingColor=enabled;
            SetError(enabled?"Click the screenshot to pick a color · Esc: cancel":"");
            Refresh(false);
        }
        bool PickColor(ShareVibeX.Core.Capture.PixelPoint point)
        {
            if(!pickingColor||rendererFrame is not {} frame)return false;
            var x=point.X-snapshot.Bounds.X;var y=point.Y-snapshot.Bounds.Y;
            if(x>=0&&x<frame.Width&&y>=0&&y<frame.Height)
            {
                var index=y*frame.Stride+x*4;
                state.SetColor(0xff000000u|((uint)frame.Bgra32[index+2]<<16)|((uint)frame.Bgra32[index+1]<<8)|frame.Bgra32[index]);
                SetColorPicking(false);
            }
            return true;
        }
        void ResolveCaptureApplication()
        {
            if(history is null)return;
            historyContext=(historyContext??history.Current) with{Application=ShareVibeX.Platform.Windows.WindowsCaptureApplication.GetName(state.Region)};
            history.Current=historyContext;
        }
        async Task Export(Window owner,bool save)
        {
            if(busy || state.IsDrawing || state.Region.IsEmpty || modes.Mode==CaptureInteractionMode.Video)return;
            busy=true;state.IsExporting=true;foreach(var host in hosts)host.Surface.Busy=true;
            try
            {
                FinishText(true);
                token.ThrowIfCancellationRequested();
                if(modes.Mode==CaptureInteractionMode.Choosing)ResolveCaptureApplication();
                snapshot=await modes.ScreenshotAsync(token);
                if(modes.Mode==CaptureInteractionMode.EditingScreenshot)Refresh(true);else rendererFrame=snapshot.Frame;
                var exported=new BitmapExportService().Crop(snapshot with {Frame=rendererFrame!},state.Region);
                string? savedPath=null;
                if(save)
                {
                    var folderPath=settings.CaptureDirectoryFor(historyContext?.CapturedAt??DateTime.Now);
                    Directory.CreateDirectory(folderPath);
                    var folder=await owner.StorageProvider.TryGetFolderFromPathAsync(folderPath);
                    var extension=ImageEncoding.Extension(settings.ImageFormat);
                    var formats=Enum.GetValues<ImageFormat>().OrderBy(format=>format==settings.ImageFormat?0:1)
                        .Select(format=>new FilePickerFileType(format.ToString().ToUpperInvariant()+Loc.T(" image")){Patterns=format==ImageFormat.Jpeg?["*.jpg","*.jpeg"]:["*."+ImageEncoding.Extension(format)]}).ToArray();
                    var destination=await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {Title=Loc.T("Save screenshot"),SuggestedFileName=ShareVibeX.App.History.CaptureHistory.FileName(historyContext??new("Desktop",DateTime.Now),extension),DefaultExtension=extension,FileTypeChoices=formats,SuggestedStartLocation=folder,ShowOverwritePrompt=true});
                    if(destination is null)return;
                    var path=destination.TryGetLocalPath()??throw new IOException(Loc.T("Choose a local file destination."));
                    await new BitmapExportService().SaveImageAsync(exported,path,ImageEncoding.FormatForPath(path),settings.ImageQuality,token);savedPath=path;
                }
                else await (publish??clipboard.CopyAsync)(owner,exported);
                if(history is not null)
                {
                    if(savedPath is not null)await history.StoreFileAsync(historyContext??history.Current,savedPath,token);
                    else await history.StoreAsync(historyContext??history.Current,ImageEncoding.Extension(settings.ImageFormat),(path,ct)=>new BitmapExportService().SaveImageAsync(exported,path,settings.ImageFormat,settings.ImageQuality,ct),token);
                }
                token.ThrowIfCancellationRequested();try{previewReady?.Invoke(exported);}catch(Exception error){DiagnosticLog.Write("Completion preview preparation: "+error.Message);}completed?.Invoke(save,savedPath);completion.TrySetResult(new CaptureSession(snapshot, state.Region));
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){completion.TrySetCanceled(token);}
            catch(Exception error){SetError(error.Message);}
            finally{busy=false;state.IsExporting=false;foreach(var host in hosts)host.Surface.Busy=false;Refresh(false);}
        }
        async Task EnterEditor(Window owner)
        {
            if(busy || state.IsDrawing || state.Region.IsEmpty)return;
            busy=true;state.IsExporting=true;foreach(var host in hosts)host.Surface.Busy=true;Refresh(false);
            try
            {
                ResolveCaptureApplication();await modes.EnterEditorAsync(token);snapshot=modes.Frozen!;rendererFrame=snapshot.Frame;
                Refresh(true);owner.Activate();hosts.First(host=>host.Window==owner).Surface.Focus();
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){completion.TrySetCanceled(token);}
            catch(Exception error){SetError(error.Message);}
            finally{busy=false;state.IsExporting=false;foreach(var host in hosts)host.Surface.Busy=false;Refresh(false);}
        }
        async Task StartRecording(RecordingSourceChoices sources)
        {
            if(record is null || busy || state.IsDrawing || state.Region.IsEmpty)return;
            busy=true;state.IsExporting=true;foreach(var host in hosts)host.Surface.Busy=true;Refresh(false);
            try
            {
                ResolveCaptureApplication();var options=settings.RecordingDefaults with{IncludeCursor=includeCursor};
                if(options.IsAnimation)sources=new(false,false);
                if(sources.Microphone&&MicrophoneProbe is not null&&!await MicrophoneProbe(options,token))
                {videoSources=sources with{Microphone=false};foreach(var bar in modebars){bar.SetMicrophoneAvailable(false);bar.SetSources(videoSources);}SetError(ShareVibeX.App.Recording.MicrophoneAvailability.UnavailableMessage);return;}
                token.ThrowIfCancellationRequested();
                if(!capture.GetMonitors().SequenceEqual(layout.Monitors))throw new InvalidOperationException(Loc.T("The display layout changed. Start a new capture."));
                foreach(var host in hosts)host.Window.Hide();
                await (waitForDesktop??WaitForDesktop)(token);
                var selected=hosts.Select((host,index)=>(Host:host,Bar:modebars[index])).First(pair=>pair.Bar.IsVisible);
                var origin=selected.Host.Window.PointToScreen(new(Canvas.GetLeft(selected.Bar),Canvas.GetTop(selected.Bar)));
                await record(new(options,state.Region,sources){CountdownSeconds=countdownSeconds,OutlineOptions=settings.SelectionDefaults,ToolbarPlacement=new(new(origin.X,origin.Y),selected.Bar.Bounds.Width,selected.Bar.Bounds.Height)},token);
                recordingStarted=true;completion.TrySetResult(null);
            }
            catch(OperationCanceledException){completion.TrySetResult(null);}
            catch(Exception error){SetError(error.Message);}
            finally
            {
                busy=false;state.IsExporting=false;
                if(!recordingStarted && !completion.Task.IsCompleted && !token.IsCancellationRequested)
                    foreach(var host in hosts){host.Window.Show();host.Surface.Busy=false;}
                Refresh(false);
            }
        }
        void SetError(string message)
        {
            foreach(var toolbar in toolbars)toolbar.Status.Text=Loc.T(message);
            foreach(var toolbar in modebars)toolbar.Status.Text=Loc.T(message);
        }
    }
    private static async Task WaitForDesktop(CancellationToken token)
    {
        await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
        await Task.Run(ShareVibeX.Platform.Windows.WindowsDesktopCapture.FlushDesktop,token);
        await Task.Delay(80,token);
    }
}

