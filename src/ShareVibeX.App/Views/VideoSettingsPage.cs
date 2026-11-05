using ShareVibeX.Core.Capture;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Views;

public sealed class VideoSettingsPage : UserControl, IDisposable
{
    private RecordingOptions draft;
    internal event Action? SettingsChanged;
    private readonly Control animationPanel,loopCount,animationLossless,animationCompression,rateControl,gifColors,gifDither;
    private readonly CheckBox customMode;
    private readonly StackPanel audioSettings;
    private readonly Control devicesSettings;
    private readonly TextBlock animationInfo=Ui.Text("",12,true);
    private readonly ComboBox captureMethod;
    public CaptureBackend GetCaptureBackend()=>captureMethod.SelectedItem is CaptureBackend backend?backend:throw new ArgumentException(Loc.T("Select a capture method."));
    private readonly Func<string?, CancellationToken, Task<FfmpegProbeResult>> probe;
    private readonly List<Action<RecordingOptions>> setters = [];
    private readonly List<NumericUpDown> numbers = [];
    private readonly TextBlock probeStatus = Ui.Text("Not tested. This test does not open capture or audio devices.", 12, true);
    private readonly TextBlock dimensions = Ui.Text("", 13, true);
    private readonly TextBox command = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 140, MaxHeight = 160, FontFamily = new FontFamily("Consolas") };
    private readonly ComboBox preset, tune, previewMode, container;
    private readonly Button testButton;
    private readonly TextBox executable;
    private bool ffmpegFound;
    private readonly Control[] generatedControls;
    private readonly Control scalePercent, divideBy, targetWidth, targetHeight, aspect;
    private readonly TextBox custom;
    private readonly Control videoBitrate, videoQuality, directShow;
    private CancellationTokenSource? pendingTest;
    private FfmpegProbeResult? tested;
    private long generation;
    private bool syncing = true, disposed;
    private CancellationTokenSource? deviceQuery;
    private long deviceGeneration;
    public string ProbeStatus => probeStatus.Text ?? "";

    public VideoSettingsPage(RecordingOptions options, Func<string?, CancellationToken, Task<FfmpegProbeResult>>? probe = null,IRecordingAudioFactory? audio=null,CaptureBackend captureBackend=CaptureBackend.Automatic)
    {
        draft = options;
        captureMethod=new ComboBox{ItemsSource=Enum.GetValues<CaptureBackend>(),SelectedItem=captureBackend,
            HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch,
            ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<CaptureBackend>((value,_)=>Ui.Text(value switch
            {CaptureBackend.Automatic=>"Automatic (prefer WGC)",CaptureBackend.WindowsGraphicsCapture=>"Windows Graphics Capture",_=>"GDI (compatibility)"}))};
        AutomationProperties.SetName(captureMethod,"Screenshot capture method");
        probeStatus.MaxLines=2;
        probeStatus.TextTrimming=TextTrimming.CharacterEllipsis;
        probeStatus.Bind(ToolTip.TipProperty,probeStatus.GetObservable(TextBlock.TextProperty));

        this.probe = probe ?? (async (path, token) =>
        {
            var snapshot=draft;var executable=FfmpegLocator.Resolve(path,ShareVibeX.Core.IO.PortablePaths.BaseDirectory);
            var result=await new FfmpegProbe(new ProcessCommandRunner()).TestAsync(executable,token);
            if(!snapshot.UseCustomCommand&&result.Capabilities.Encoders.Contains(snapshot.EncoderName))
                await new FfmpegEncoderProbe(new ProcessCommandRunner()).ValidateAsync(executable,snapshot,token);
            if(snapshot.IsAnimation)
            {
                if(!result.Capabilities.Encoders.Contains("ffv1"))throw new InvalidOperationException(Loc.F("The selected FFmpeg does not include encoder {0}. Choose another encoder or FFmpeg build.","ffv1"));
                await new FfmpegAnimationProbe(new ProcessCommandRunner()).ValidateAsync(executable,snapshot,token);
            }
            return result;
        });
        executable = Text("FFmpeg executable", o => o.FfmpegPath ?? "", (o, v) => o with { FfmpegPath = Empty(v) });
        executable.IsReadOnly=true;
        testButton=Ui.Button("Test",async()=>
        {
            RefreshExecutable();
            if(ffmpegFound)await TestAsync();
            else
            {
                try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://ffmpeg.org/download.html"){UseShellExecute=true});}
                catch(Exception error){probeStatus.Text=error.Message;}
            }
        });
        SetName(testButton,"Test FFmpeg");
        var automatic=new MenuItem{Header=Loc.T("Use automatic FFmpeg discovery")};
        automatic.Click+=(_,_)=>executable.Text="";
        executable.ContextMenu=new ContextMenu{ItemsSource=new[]{automatic}};
        async Task BrowseAsync()
        {
            try
            {
                var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storage is null) return;
                var files = await storage.OpenFilePickerAsync(new() { Title = Loc.T("FFmpeg executable"), AllowMultiple = false,
                    FileTypeFilter = [new(Loc.T("Executable")) { Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"] }] });
                if (!disposed && files.Count > 0) executable.Text = files[0].TryGetLocalPath();
            }
            catch (Exception error) { if (!disposed) probeStatus.Text = error.Message; }
        }
        executable.AddHandler(PointerPressedEvent,async(_,e)=>
        {
            if(e.GetCurrentPoint(executable).Properties.PointerUpdateKind!=PointerUpdateKind.LeftButtonPressed)return;
            e.Handled=true;await BrowseAsync();
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        executable.KeyDown+=async(_,e)=>{if(e.Key==Key.Enter){e.Handled=true;await BrowseAsync();}};
        var tool=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=6,Children={executable,testButton}};
        Grid.SetColumn(testButton,1);
        var encoder = Choice("Video encoder", Enum.GetValues<VideoEncoder>().Where(e=>e!=VideoEncoder.Ffv1).ToArray(), o => o.Encoder, (o, v) => o with { Encoder = v });
        encoder.MaxDropDownHeight = 220;
        preset = Choice("Encoder preset", RecordingValidation.Presets(options.Encoder), o => o.Preset, (o, v) => o with { Preset = v });
        tune = Choice("Encoder tune", RecordingValidation.Tunes(options.Encoder), o => o.Tune, (o, v) => o with { Tune = v });
        tune.ItemTemplate=new FuncDataTemplate<string>((value,_)=>Ui.Text(value==""?"Auto":value,13));
        var rate = Choice("Rate control", Enum.GetValues<VideoRateControl>(), o => o.RateControl, (o, v) => o with { RateControl = v });
        rateControl=rate;
        loopCount=Number("Animation loop count",0,65535,o=>o.AnimationLoopCount,(o,v)=>o with{AnimationLoopCount=(int)v});
        animationLossless=Toggle("Lossless WebP",o=>o.AnimationLossless,(o,v)=>o with{AnimationLossless=v});
        animationCompression=Number("Animation compression",0,9,o=>o.AnimationCompressionLevel,(o,v)=>o with{AnimationCompressionLevel=(int)v});
        gifColors=Number("GIF colors",4,256,o=>o.GifColors,(o,v)=>o with{GifColors=(int)v});
        gifDither=Choice("GIF dithering",Enum.GetValues<GifDither>(),o=>o.GifDither,(o,v)=>o with{GifDither=v});
        animationPanel=Panel(Field("GIF colors",gifColors),Field("GIF dithering",gifDither,Loc.T("Dithering can increase file size. Lower FPS or reduce the output size for a smaller GIF.")),Field("Animation loops",loopCount),Field("Lossless WebP",animationLossless),Field("Compression",animationCompression),animationInfo);
        videoQuality = Number("Video quality", 0, 100, o => o.Quality, (o, v) => o with { Quality = (int)v });
        videoBitrate = Number("Video bitrate", 100, 500000, o => o.VideoBitrateKbps, (o, v) => o with { VideoBitrateKbps = (int)v });
        var additional = Text("Additional FFmpeg arguments", o => o.AdditionalArguments, (o, v) => o with { AdditionalArguments = v });
        generatedControls = [encoder, preset, tune, rate, videoQuality, videoBitrate, additional];
        directShow = Text("DirectShow audio device", o => o.DirectShowDevice, (o, v) => o with { DirectShowDevice = v });
        scalePercent = Number("Scale percentage", 0.01m, 1600, o => (decimal)o.ScalePercent, (o, v) => o with { ScalePercent = (double)v }, 5);
        divideBy = Number("Divide dimensions by", 0.01m, 1000, o => (decimal)o.DivideBy, (o, v) => o with { DivideBy = (double)v }, 0.1m);
        targetWidth = Number("Target width", 0, 16384, o => o.TargetWidth, (o, v) => o with { TargetWidth = (int)v });
        targetHeight = Number("Target height", 0, 16384, o => o.TargetHeight, (o, v) => o with { TargetHeight = (int)v });
        aspect = Toggle("Keep aspect ratio", o => o.KeepAspectRatio, (o, v) => o with { KeepAspectRatio = v });
        custom = Text("Custom FFmpeg template", o => o.CustomCommand, (o, v) => o with { CustomCommand = v });
        custom.TextWrapping = TextWrapping.Wrap; custom.MinHeight = 84; custom.MaxHeight = 110;
        var reset = Ui.Button(Loc.T("Reset recording options"), () => Apply(new RecordingOptions())); SetName(reset, "Reset recording options");
        previewMode = new ComboBox { ItemsSource = new[] { Loc.T("Silent / GIF-style"), Loc.T("With audio") }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        SetName(previewMode, "Command preview audio mode"); previewMode.SelectionChanged += (_, _) => Update();
        var cursor = Toggle("Include cursor", o => o.IncludeCursor, (o,v) => o with { IncludeCursor = v }); ToolTip.SetTip(cursor,Loc.T("Include cursor"));
        var fps = Number("Frame rate", 1, 240, o => o.FrameRate, (o,v) => o with { FrameRate = (int)v });
        fps.MinWidth=70;
        fps.ShowButtonSpinner=false;
        ToolTip.SetTip(fps,Loc.T("Scroll to change the frame rate."));
        fps.AddHandler(PointerWheelChangedEvent,(_,e)=>
        {
            if(e.Delta.Y==0)return;
            fps.Value=Math.Clamp((fps.Value??draft.FrameRate)+Math.Sign(e.Delta.Y),fps.Minimum,fps.Maximum);
            e.Handled=true;
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var fpsCursor = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8, Children = { fps, cursor } }; Grid.SetColumn(cursor, 1);
        var qualityValue = new Grid { Children = { videoQuality, videoBitrate } };
        var qualityControls = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 8, Children = { rate, qualityValue } }; Grid.SetColumn(qualityValue, 1);
        container=Choice("Video container",RecordingValidation.Containers(options.Encoder),o=>o.Container,(o,v)=>o with{Container=v,AudioEncoder=v==VideoContainer.Webm?AudioEncoder.Libopus:v==VideoContainer.Mp4?AudioEncoder.Aac:o.AudioEncoder,AudioSampleRate=v==VideoContainer.Webm?48000:o.AudioSampleRate});
        var videoColumn = Panel(
            Field("Capture method",captureMethod,Loc.T("Applies to screenshots and structured video recording. Custom FFmpeg commands keep their explicit source.")),
            Field("Video encoder", encoder, Loc.T("Hardware encoder presence is not a hardware initialization test.")),
            Field("Rate / value", qualityControls, Loc.T("Quality range depends on the encoder; lower keeps more detail. VP8 quality also uses a target bitrate. Bitrate: kbps.")),
            Field("Preset", preset), Field("Tune", tune), Field("FPS / cursor", fpsCursor),
            Field("Container", container),animationPanel);
        var audioColumn = Panel(
            Field("Audio source", Choice("Audio source", Enum.GetValues<AudioSource>(), o => o.AudioSource, (o,v) => o with { AudioSource = v })),
            Field("Audio encoder", Choice("Audio encoder", Enum.GetValues<AudioEncoder>(), o => o.AudioEncoder, (o,v) => o with { AudioEncoder = v })),
            Field("Bitrate (kbps)", Number("Audio bitrate", 32, 512, o => o.AudioBitrateKbps, (o,v) => o with { AudioBitrateKbps = (int)v })),
            Field("Sample rate", Choice("Audio sample rate", new[] { 44100, 48000 }, o => o.AudioSampleRate, (o,v) => o with { AudioSampleRate = v })),
            Field("Channels", Choice("Audio channels", new[] { 1, 2 }, o => o.AudioChannels, (o,v) => o with { AudioChannels = v })));
        audioSettings=audioColumn;
        var scaling = Columns(
            Panel(Field("Scale mode", Choice("Scaling mode", Enum.GetValues<ScaleMode>(), o => o.ScaleMode, (o,v) => o with { ScaleMode = v })),
                Field("Percent", scalePercent, Loc.T("50 means half width and half height.")), Field("Divide by", divideBy, Loc.T("1.5 means width/1.5 and height/1.5.")),
                Field("Algorithm", Choice("Scaling algorithm", Enum.GetValues<ScaleAlgorithm>(), o => o.ScaleAlgorithm, (o,v) => o with { ScaleAlgorithm = v }))),
            Panel(Field("Target width", targetWidth), Field("Target height", targetHeight), Field("Aspect ratio", aspect),
                Field("Limit (sec)", Number("Recording duration", 0, 86400, o => (decimal)o.DurationSeconds, (o,v) => o with { DurationSeconds = (double)v }), Loc.T("Zero means stop manually. Custom mode needs output -t $duration$ when positive."))));
        ToolTip.SetTip(command,Loc.T("Example only: 1920 × 1080 at (0, 0); pipe and filename are placeholders."));
        var preview = Panel(previewMode,dimensions,command);
        var deviceStatus=Ui.Text("Use the Windows default endpoints or refresh to choose active devices. Refresh lists endpoints; only Record starts capture.",12,true);
        var playback=DeviceCombo("Playback device",true);
        var microphone=DeviceCombo("Microphone device",false);
        var refreshDevices=Ui.Button(Loc.T("Refresh audio devices"),async()=>
        {
            if(audio is null){deviceStatus.Text=Loc.T("Native audio is unavailable on this platform.");return;}
            deviceQuery?.Cancel();var generation=++deviceGeneration;
            using var cancellation=new CancellationTokenSource();deviceQuery=cancellation;
            try
            {
                var devices=await audio.GetDevicesAsync(cancellation.Token);
                if(disposed||generation!=deviceGeneration)return;
                syncing=true;
                Populate(playback,devices.Where(d=>d.Kind==RecordingAudioDeviceKind.Playback),draft.PlaybackDeviceId);
                Populate(microphone,devices.Where(d=>d.Kind==RecordingAudioDeviceKind.Microphone),draft.MicrophoneDeviceId);
                syncing=false;deviceStatus.Text=Loc.F("{0} active endpoints. System + microphone are mixed to stereo; silent recording never opens an audio device.",devices.Count);
            }
            catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
            catch(Exception error){if(!disposed&&generation==deviceGeneration)deviceStatus.Text=error.Message;}
            finally{if(generation==deviceGeneration)deviceQuery=null;syncing=false;}
        });
        AutomationProperties.SetName(refreshDevices,"Refresh audio devices");
        var devicesPage=Panel(Field("Playback",playback),Field("Microphone",microphone),Field("DirectShow audio device",directShow,Loc.T("Exact DirectShow audio device name, when selected. Select native endpoints on the Devices tab.")),refreshDevices,deviceStatus);
        devicesSettings=devicesPage;
        var customToggle = Toggle("Use custom command", o => o.UseCustomCommand, (o,v) => o with { UseCustomCommand = v }); customToggle.Content = Loc.T("Use custom command");customMode=customToggle;
        var templateHelp=Ui.Text("Custom mode overrides encoder, quality, preset, tune and additional arguments. Placeholders: $fps$, $area_x$, $area_y$, $area_width$, $area_height$, $cursor$, $scale_filter$, $duration$, one final $output$. Standalone $audio_input$ and $audio_options$ adapt both audio modes. No shell; extra outputs and unsupported options are rejected.",12,true);
        ToolTip.SetTip(customToggle,templateHelp);
        reset.HorizontalAlignment = HorizontalAlignment.Right;
        var timings=Toggle("Show video timings",o=>o.ShowVideoTimings,(o,v)=>o with{ShowVideoTimings=v});timings.Content=Loc.T("Show video timings");
        var advanced=new Expander{Header=Loc.T("Advanced FFmpeg options"),HorizontalAlignment=HorizontalAlignment.Stretch,
            Content=Panel(Ui.Text("Additional command line arguments",13),additional,customToggle,custom,reset)};
        AutomationProperties.SetName(advanced,"Advanced FFmpeg options");
        var tabs = new TabControl { ItemsSource = new[] {
            new TabItem { Header = Ui.Text("Encoding",14), Content = Columns(videoColumn, audioColumn) },
            new TabItem { Header = Ui.Text("Scaling",14), Content = scaling },new TabItem{Header=Ui.Text("Devices",14),Content=devicesPage}, new TabItem { Header = Ui.Text("Preview",14), Content = Columns(preview,Panel(timings,advanced)) } }, SelectedIndex = 0 };
        AutomationProperties.SetName(tabs, "Video settings tabs");
        var root = Panel(tool, probeStatus, tabs);
        root.Spacing = 6; Content = root;
        Apply(options);RefreshExecutable();
        captureMethod.SelectionChanged+=(_,_)=>{if(captureMethod.SelectedItem is CaptureBackend)SettingsChanged?.Invoke();};
    }

    public RecordingOptions GetOptions()
    {
        if (numbers.Any(number => number.Value is null)) throw new ArgumentException(Loc.T("Fill all numeric recording fields."));
        RecordingValidation.Validate(draft);
        FfmpegCommandBuilder.Build(draft, new(0, 0, 1920, 1080), false, PreviewOutput());
        if(!draft.IsAnimation)FfmpegCommandBuilder.Build(draft, new(0, 0, 1920, 1080), true, PreviewOutput(), AudioInput());
        return draft;
    }

    public async Task TestAsync()
    {
        if (disposed) return;
        pendingTest?.Cancel(); var request = ++generation;
        using var cancellation = new CancellationTokenSource(); pendingTest = cancellation;
        testButton.IsEnabled = false; probeStatus.Text = Loc.T("Testing FFmpeg capabilities…"); tested = null;
        try
        {
            var result = await probe(draft.FfmpegPath, cancellation.Token);
            if (disposed || request != generation) return;
            tested = result; UpdateProbeStatus();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) { if (!disposed && request == generation) probeStatus.Text = error.Message; }
        finally { if (request == generation) { pendingTest = null; if (!disposed) testButton.IsEnabled = true; } }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; generation++; pendingTest?.Cancel(); pendingTest = null;deviceGeneration++;deviceQuery?.Cancel();deviceQuery=null;
    }
    private void Apply(RecordingOptions options)
    {
        syncing = true; var pathChanged = draft.FfmpegPath != options.FfmpegPath || draft.Encoder != options.Encoder; draft = options;
        preset.ItemsSource = RecordingValidation.Presets(draft.Encoder); tune.ItemsSource = RecordingValidation.Tunes(draft.Encoder);
        ((NumericUpDown)videoQuality).Minimum=RecordingValidation.QualityMinimum(draft.Encoder);
        ((NumericUpDown)videoQuality).Maximum=RecordingValidation.QualityMaximum(draft.Encoder);
        ((NumericUpDown)animationCompression).Maximum=draft.Encoder==VideoEncoder.Apng?9:6;
        container.ItemsSource=RecordingValidation.Containers(draft.Encoder);
        foreach (var setter in setters) setter(options);
        syncing = false; if (pathChanged) InvalidateProbe(); Update();
        SettingsChanged?.Invoke();
    }
    private void Change(RecordingOptions options)
    {
        if (syncing || disposed || options==draft) return;
        var pathChanged = options.FfmpegPath != draft.FfmpegPath; var encoderChanged = options.Encoder != draft.Encoder;
        if (encoderChanged)
        {
            Apply(draft.SwitchEncoder(options.Encoder));return;
        }
        if(options.Container!=draft.Container){Apply(options);return;}
        draft = options;
        if (pathChanged) InvalidateProbe(); Update();
        SettingsChanged?.Invoke();
    }
    private void RefreshExecutable()
    {
        try
        {
            var path=FfmpegLocator.Resolve(draft.FfmpegPath,ShareVibeX.Core.IO.PortablePaths.BaseDirectory);
            executable.PlaceholderText=path;
            ToolTip.SetTip(executable,path);
            ffmpegFound=true;
        }
        catch(Exception error) when(error is FileNotFoundException or ArgumentException or NotSupportedException)
        {
            executable.PlaceholderText=Loc.T("FFmpeg not found");
            ToolTip.SetTip(executable,Loc.T("Choose an FFmpeg executable"));
            ffmpegFound=false;
        }
        testButton.Content=Loc.T(ffmpegFound?"Test":"Install");
    }
    private void InvalidateProbe()
    {
        generation++; pendingTest?.Cancel(); pendingTest = null; tested = null;
        RefreshExecutable();testButton.IsEnabled = true; probeStatus.Text = Loc.T("Not tested. The FFmpeg path or encoder changed.");
    }
    private void Update()
    {
        if (syncing || disposed) return;
        animationPanel.IsVisible=draft.IsAnimation;
        if(gifColors.Parent is Control colorRow)colorRow.IsVisible=draft.Encoder==VideoEncoder.Gif;
        if(gifDither.Parent is Control ditherRow)ditherRow.IsVisible=draft.Encoder==VideoEncoder.Gif;
        if(container.Parent is Control containerRow)containerRow.IsVisible=RecordingValidation.Containers(draft.Encoder).Length>1;
        foreach(var row in audioSettings.Children.Take(audioSettings.Children.Count-1))row.IsEnabled=!draft.IsAnimation;
        devicesSettings.IsEnabled=!draft.IsAnimation;
        animationLossless.IsVisible=draft.Encoder==VideoEncoder.LibwebpAnim;
        if(animationLossless.Parent is Control losslessRow)losslessRow.IsVisible=animationLossless.IsVisible;
        animationCompression.IsVisible=draft.Encoder is VideoEncoder.LibwebpAnim or VideoEncoder.Apng;
        if(animationCompression.Parent is Control compressionRow)compressionRow.IsVisible=animationCompression.IsVisible;
        rateControl.IsVisible=!draft.IsAnimation;
        animationInfo.Text=Loc.T("Animations are silent. 0 loops means infinite playback. GIF counts repeats; APNG counts plays. WebP quality: higher is better. Temporary lossless files can be large.");
        customMode.IsEnabled=!draft.IsAnimation;previewMode.IsEnabled=!draft.IsAnimation;
        foreach (var control in generatedControls) control.IsEnabled = !draft.UseCustomCommand;
        videoQuality.IsEnabled = !draft.UseCustomCommand && draft.RateControl == VideoRateControl.Quality;
        videoBitrate.IsEnabled = !draft.UseCustomCommand && draft.RateControl == VideoRateControl.Bitrate;
        preset.IsEnabled = !draft.UseCustomCommand && RecordingValidation.Presets(draft.Encoder).Length>1;
        tune.IsEnabled = !draft.UseCustomCommand && RecordingValidation.Tunes(draft.Encoder).Length>1;
        ToolTip.SetTip(preset,Loc.F("Auto uses {0}.",RecordingValidation.AutoPreset(draft.Encoder))+" "+Loc.T(draft.Encoder switch
        {
            VideoEncoder.H264Nvenc or VideoEncoder.HevcNvenc=>"NVENC P1 prioritizes encoding speed. P7 prioritizes compression efficiency.",
            VideoEncoder.H264Amf or VideoEncoder.HevcAmf=>"AMF speed prioritizes throughput, balanced is a compromise, and quality prioritizes compression efficiency.",
            VideoEncoder.H264Qsv or VideoEncoder.HevcQsv=>"Quick Sync veryfast favors speed. Slower presets spend more time on compression.",
            VideoEncoder.Libvpx or VideoEncoder.LibvpxVp9=>"VP8 and VP9 use cpu-used. Higher values encode faster and generally compress less efficiently.",
            VideoEncoder.LibaomAv1=>"AOM AV1 uses cpu-used. Lower values spend more time on compression.",
            VideoEncoder.Libsvtav1=>"SVT-AV1 uses numbered presets. Lower numbers favor compression efficiency and higher numbers favor speed.",
            _=>"Ultrafast favors speed. Slower x264 and x265 presets spend more CPU time to compress more efficiently."
        }));
        var tuneHelp=draft.Encoder switch
        {
            VideoEncoder.Libx264=>Loc.T("Film, animation, grain and stillimage optimize for the corresponding content. Fastdecode reduces decoding work. Zerolatency minimizes encoder buffering."),
            VideoEncoder.Libx265=>Loc.T("PSNR and SSIM optimize for their respective test metrics. Grain preserves texture, animation targets animated content, fastdecode reduces decoding work, and zerolatency reduces buffering."),
            VideoEncoder.H264Nvenc or VideoEncoder.HevcNvenc=>Loc.T("HQ prioritizes quality, LL reduces latency, ULL reduces it further, and lossless selects lossless tuning."),
            _=>null
        };
        ToolTip.SetTip(tune,tuneHelp is null?null:Loc.T("Auto uses the encoder default without additional tuning.")+" "+tuneHelp);
        if(preset.Parent is Control presetRow)presetRow.IsVisible=RecordingValidation.Presets(draft.Encoder).Length>1;
        if(tune.Parent is Control tuneRow)tuneRow.IsVisible=RecordingValidation.Tunes(draft.Encoder).Length>1;
        custom.IsEnabled = custom.IsVisible = draft.UseCustomCommand;
        videoQuality.IsVisible = draft.RateControl == VideoRateControl.Quality; videoBitrate.IsVisible = !videoQuality.IsVisible || draft.Encoder==VideoEncoder.Libvpx;
        if(draft.Encoder==VideoEncoder.Libvpx)videoBitrate.IsEnabled=!draft.UseCustomCommand;
        scalePercent.IsEnabled = draft.ScaleMode == ScaleMode.Percentage; divideBy.IsEnabled = draft.ScaleMode == ScaleMode.DivideBy;
        targetWidth.IsEnabled = targetHeight.IsEnabled = aspect.IsEnabled = draft.ScaleMode == ScaleMode.TargetSize;
        if(directShow.Parent is Control deviceField)deviceField.IsVisible=draft.AudioSource==AudioSource.DirectShow;
        try
        {
            if (numbers.Any(number => number.Value is null)) throw new ArgumentException(Loc.T("Fill all numeric recording fields."));
            var result = FfmpegCommandBuilder.Build(draft, new(0, 0, 1920, 1080), !draft.IsAnimation && previewMode.SelectedIndex == 1, PreviewOutput(), AudioInput());
            dimensions.Text = Loc.F("Example output: {0} × {1} px; odd dimensions are padded, not cropped.",result.Width,result.Height) +
                (draft.UseCustomCommand ? Loc.T(" Custom filters can change this estimate.") : "");
            command.Text = FfmpegArguments.Display(new[] { draft.FfmpegPath ?? "<auto-detected ffmpeg>" }.Concat(result.Arguments));
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or NotSupportedException)
        { dimensions.Text = error.Message; command.Text = Loc.T("No valid command. Correct the options above."); }
        if(draft.IsAnimation)
        {
            rateControl.IsEnabled=false;videoBitrate.IsVisible=false;videoBitrate.IsEnabled=false;
            videoQuality.IsVisible=draft.Encoder==VideoEncoder.LibwebpAnim;
            videoQuality.IsEnabled=videoQuality.IsVisible&&!draft.AnimationLossless;
        }
        if (tested is not null) UpdateProbeStatus();
    }
    private void UpdateProbeStatus()
    {
        if (tested is null) return;
        var missing = tested.Capabilities.Missing(draft, !draft.IsAnimation && previewMode.SelectedIndex == 1);
        probeStatus.Text = tested.Path + "\n" + tested.Capabilities.Version + "\n" +
            (draft.UseCustomCommand ? Loc.T("Capability query succeeded. Custom templates need separate runtime validation.") :
                missing.Count == 0 ? Loc.T("Required listed capabilities are present. Hardware initialization and capture are not tested.") : Loc.T("Missing: ") + string.Join(", ", missing));
        var wgcMissing=tested.Capabilities.MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend.WindowsGraphicsCapture);
        probeStatus.Text+="\n"+(wgcMissing.Count==0?Loc.T("WGC video prerequisites are listed. Hardware initialization and capture are not tested."):
            Loc.F("WGC video prerequisites missing: {0}.",string.Join(", ",wgcMissing)));
    }
    private IReadOnlyList<string> AudioInput() => draft.AudioSource == AudioSource.DirectShow
        ? ["-thread_queue_size", "1024", "-f", "dshow", "-i", "audio=" + (string.IsNullOrWhiteSpace(draft.DirectShowDevice) ? "<selected DirectShow device>" : draft.DirectShowDevice)]
        : ["-thread_queue_size", "1024", "-f", "f32le", "-ar", "48000", "-ac", "2", "-i", @"\\.\pipe\ShareVibeX-audio-preview"];
    private string PreviewOutput() => Path.Combine(draft.OutputDirectory ?? "Captures", "preview.partial." + draft.Extension);
    private TextBox Text(string name, Func<RecordingOptions, string> get, Func<RecordingOptions, string, RecordingOptions> set)
    {
        var box = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch }; SetName(box, name);
        setters.Add(o => box.Text = get(o)); box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Change(set(draft, box.Text ?? "")); }; return box;
    }
    private ComboBox Choice<T>(string name, IEnumerable<T> items, Func<RecordingOptions, T> get, Func<RecordingOptions, T, RecordingOptions> set)
    {
        var box = new ComboBox { ItemsSource = items, HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<T>((value, _) => Ui.Text(OptionLabel(value), 13)) }; SetName(box, name);
        setters.Add(o => box.SelectedItem = get(o));
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is T value) Change(set(draft, value));
        }; return box;
    }
    private NumericUpDown Number(string name, decimal min, decimal max, Func<RecordingOptions, decimal> get, Func<RecordingOptions, decimal, RecordingOptions> set, decimal step = 1)
    {
        var number = new NumericUpDown { Minimum = min, Maximum = max, Increment = step, FormatString = "0.##", HorizontalAlignment = HorizontalAlignment.Stretch }; SetName(number, name);
        numbers.Add(number); setters.Add(o => number.Value = get(o));
        number.ValueChanged += (_, _) => { if (number.Value is decimal value) Change(set(draft, value)); else Update(); }; return number;
    }
    private CheckBox Toggle(string name, Func<RecordingOptions, bool> get, Func<RecordingOptions, bool, RecordingOptions> set)
    {
        var box = new CheckBox(); SetName(box, name); setters.Add(o => box.IsChecked = get(o));
        box.PropertyChanged += (_, e) => { if (e.Property == ToggleButton.IsCheckedProperty) Change(set(draft, box.IsChecked == true)); }; return box;
    }
    private static void SetName(Control control, string name) => AutomationProperties.SetName(control, name);
    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private ComboBox DeviceCombo(string name,bool playback)
    {
        var combo=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};SetName(combo,name);
        setters.Add(o=>SelectDevice(combo,playback?o.PlaybackDeviceId:o.MicrophoneDeviceId));
        combo.SelectionChanged+=(_,_)=>
        {
            if(combo.SelectedItem is DeviceChoice item)Change(playback?draft with{PlaybackDeviceId=item.Id}:draft with{MicrophoneDeviceId=item.Id});
        };
        Populate(combo,[],playback?draft.PlaybackDeviceId:draft.MicrophoneDeviceId);return combo;
    }
    private static void Populate(ComboBox combo,IEnumerable<RecordingAudioDevice> devices,string? selected)
    {
        var choices=new List<DeviceChoice>{new(null,Loc.T("Windows default"))};
        choices.AddRange(devices.Select(device=>new DeviceChoice(device.Id,device.Name+(device.IsDefault?Loc.T(" (default)"):""))));
        if(!string.IsNullOrEmpty(selected)&&choices.All(c=>c.Id!=selected))choices.Add(new(selected,Loc.T("Saved endpoint (refresh to verify)")));
        combo.ItemsSource=choices;combo.SelectedItem=choices.First(c=>c.Id==selected||c.Id is null&&string.IsNullOrEmpty(selected));
    }
    private static void SelectDevice(ComboBox combo,string? id)
    {
        var choices=combo.ItemsSource!.Cast<DeviceChoice>().ToList();
        if(!string.IsNullOrEmpty(id)&&choices.All(c=>c.Id!=id)){choices.Add(new(id,Loc.T("Saved endpoint (refresh to verify)")));combo.ItemsSource=choices;}
        combo.SelectedItem=choices.First(c=>c.Id==id||c.Id is null&&string.IsNullOrEmpty(id));
    }
    private sealed record DeviceChoice(string? Id,string Name){public override string ToString()=>Name;}
    private static string OptionLabel<T>(T value) => value switch
    {
        VideoEncoder.Libx264 => "H.264 / x264", VideoEncoder.Libx265 => "H.265 / x265",
        VideoEncoder.H264Nvenc => "H.264 / NVENC", VideoEncoder.HevcNvenc => "H.265 / NVENC",
        VideoEncoder.Gif=>"GIF",VideoEncoder.LibwebpAnim=>Loc.T("Animated WebP"),VideoEncoder.Apng=>"APNG",
        VideoEncoder.HevcQsv=>"H.265 / QSV",VideoEncoder.HevcAmf=>"H.265 / AMF",
        VideoEncoder.Libvpx=>"VP8 / libvpx",VideoEncoder.LibvpxVp9=>"VP9 / libvpx",
        VideoEncoder.Libsvtav1=>"AV1 / SVT-AV1",VideoEncoder.LibaomAv1=>"AV1 / libaom",VideoEncoder.Libxvid=>"MPEG-4 / Xvid",
        VideoEncoder.H264Qsv => "H.264 / QSV", VideoEncoder.H264Amf => "H.264 / AMF",
        AudioSource.System => Loc.T("System playback"), AudioSource.Microphone => Loc.T("Microphone"),
        AudioSource.SystemAndMicrophone => Loc.T("System + mic"), AudioSource.DirectShow => Loc.T("DirectShow device"),
        ScaleMode.DivideBy => Loc.T("Divide by factor"), ScaleMode.TargetSize => Loc.T("Target size"),
        VideoContainer.Gif=>"GIF",VideoContainer.AnimatedWebp=>Loc.T("Animated WebP"),VideoContainer.Apng=>"APNG",VideoContainer.Webm=>"WebM",VideoContainer.Mp4 => "MP4", VideoContainer.Matroska => "MKV (Matroska)",
        AudioEncoder.Aac => "AAC", AudioEncoder.Libopus => "Opus", "" => Loc.T("None"), "auto" => Loc.T("Auto"),
        GifDither.Sierra => "Sierra",
        GifDither.Bayer => "Bayer",
        GifDither.None => Loc.T("None"),
        _ => value?.ToString() ?? ""
    };
    private static StackPanel Panel(params Control[] controls) { var panel = Ui.Stack(controls); panel.Spacing = 3; return panel; }
    private static Grid Field(string title, Control value, string? help = null)
    {
        var label = Ui.Text(title,12);label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;
        label.Classes.Add("video-field-label");ToolTip.SetTip(label,Loc.T(title));
        value.HorizontalAlignment=HorizontalAlignment.Stretch;
        var grid=new Grid{ColumnDefinitions=new("*,1.8*"),ColumnSpacing=6,Children={label,value}};
        label.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(value,1);if(help is not null)ToolTip.SetTip(grid,Loc.T(help));return grid;
    }
    private static Grid Columns(Control left, Control right)
    {
        var grid = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 20, Margin = new Thickness(0, 6, 0, 0), Children = { left, right } };
        Grid.SetColumn(right, 1); return grid;
    }
}

