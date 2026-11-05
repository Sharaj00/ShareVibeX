using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class LiveOverlayIntegrationTests(UiTestFixture ui)
{
    [Fact]
    public Task NonActivatingSelectionStillSupportsMouseSelectionAndQuickCopy()=>ui.Run(async()=>
    {
        using var test=new Harness(showActivated:false);
        var running=test.Start();
        Assert.True(test.Window.IsVisible);
        Assert.False(test.Window.ShowActivated);
        test.SelectRegion();
        test.Window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});
        Assert.NotNull(await running.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.NotNull(test.Copied);
    });
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task VideoStartsFromSameRegionWithOverlaysHiddenAndNoStillCapture(bool withAudio) => ui.Run(async () =>
    {
        RecordingStartRequest? requested=null;var hidden=false;
        Harness? test=null;
        using(test=new Harness((request,_)=>{requested=request;hidden=!test!.Window.IsVisible;return Task.CompletedTask;}))
        {
            var running=test.Start();test.SelectRegion();test.Click("Video");
            if(!withAudio)test.Window.GetVisualDescendants().OfType<CaptureModeToolbar>().Single().SetSources(new(false,false));
            var cursor=test.Window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
                .Single(b=>Avalonia.Automation.AutomationProperties.GetName(b)=="Include cursor");
            Assert.True(cursor.IsChecked);
            cursor.IsChecked=false;
            test.Window.UpdateLayout();
            var countdown=test.Window.GetVisualDescendants().OfType<Border>()
                .Single(b=>Avalonia.Automation.AutomationProperties.GetName(b)=="Recording countdown");
            var wheelPoint=countdown.TranslatePoint(new Point(countdown.Bounds.Width/2,countdown.Bounds.Height/2),test.Window)!.Value;
            test.Window.MouseWheel(wheelPoint,new Vector(0,1));
            Assert.Equal("1s",Assert.IsType<TextBlock>(countdown.Child).Text);
            test.Click("Record");
            Assert.Null(await running.WaitAsync(TimeSpan.FromSeconds(3)));Assert.NotNull(requested);
            Assert.False(requested.Options.IncludeCursor);Assert.Equal(1,requested.CountdownSeconds);
            Assert.Equal(withAudio,requested.Sources.WithAudio);Assert.Equal(new ShareVibeX.Core.Capture.PixelRect(100,150,140,80),requested.Region);
            Assert.True(hidden);Assert.Equal(0,test.Source.Captures);
            Assert.NotNull(requested.ToolbarPlacement);Assert.True(requested.ToolbarPlacement.Width>0);Assert.True(requested.ToolbarPlacement.Height>0);
        }
    });
    [Fact]
    public Task RecordingStartupFailureRestoresLiveRegionForRetry() => ui.Run(async () =>
    {
        using var test=new Harness((_,_)=>throw new IOException("encoder unavailable"));
        var running=test.Start();test.SelectRegion();test.Click("Video");test.Click("Record");
        Assert.True(test.Window.IsVisible);Assert.True(test.Surface.IsLiveSelection);
        Assert.Contains("encoder unavailable",test.Window.GetVisualDescendants().OfType<CaptureModeToolbar>().Single().Status.Text);
        test.Window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});
        Assert.Null(await running.WaitAsync(TimeSpan.FromSeconds(3)));
    });
    [Fact]
    public Task RecordingCanceledByItsOwnControlsEndsTheSelectionWithoutAnOuterCancellation() => ui.Run(async () =>
    {
        using var test=new Harness((_,_)=>Task.FromException(new OperationCanceledException("Canceled from recording controls.")));
        var running=test.Start();test.SelectRegion();test.Click("Video");test.Click("Record");
        try
        {
            await Task.WhenAny(running,Task.Delay(1000));
            Assert.True(running.IsCompleted,"Recording cancellation should close the selection, not restore the live toolbar.");
            Assert.Null(await running);Assert.False(test.Window.IsVisible);
        }
        finally{if(!running.IsCompleted)test.Window.Close();}
    });
    [Fact]
    public Task ModeIconsStayHiddenUntilSelectionGestureFinishes() => ui.Run(async () =>
    {
        using var test = new Harness(); var running = test.Start();
        test.Window.MouseDown(new Point(100, 150), MouseButton.Left); test.Window.MouseMove(new Point(240, 230));
        var toolbar = test.Window.GetVisualDescendants().OfType<CaptureModeToolbar>().Single();
        Assert.False(toolbar.IsVisible);
        test.Window.MouseUp(new Point(240, 230), MouseButton.Left); Assert.True(toolbar.IsVisible);
        test.Window.UpdateLayout();
        Assert.InRange(toolbar.Bounds.Center.X,169,171);
        test.Click("Video");
        test.Window.UpdateLayout();
        Assert.InRange(toolbar.Bounds.Center.X,169,171);
        test.Window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Null(await running.WaitAsync(TimeSpan.FromSeconds(3))); Assert.Equal(0, test.Source.Captures);
    });
    [Theory]
    [InlineData(Key.Enter, KeyModifiers.None)]
    [InlineData(Key.C, KeyModifiers.Control)]
    public Task QuickShortcutCopiesFreshUneditedRegion(Key key, KeyModifiers modifiers) => ui.Run(async () =>
    {
        using var test = new Harness();
        var running = test.Start(); test.SelectRegion();
        Assert.Equal(0, test.Source.Captures); Assert.True(test.Surface.IsLiveSelection);
        Assert.Null(test.Surface.Image);
        test.Source.Value = 147;
        test.Window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
        var result = await running.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.NotNull(result); Assert.Equal(1, test.Source.Captures);
        Assert.NotNull(test.Copied); Assert.Equal(140, test.Copied.Width); Assert.Equal(80, test.Copied.Height);
        Assert.Equal(147, test.Copied.Bgra32[0]); Assert.True(test.Source.AllHidden);
        Assert.False(test.Window.IsVisible);
    });
    [Fact]
    public Task EditorEntryCapturesOnceThenCopyUsesFrozenImage() => ui.Run(async () =>
    {
        using var test = new Harness(); var running = test.Start(); test.SelectRegion();
        test.Source.Value = 87; test.Click("Edit screenshot");
        Assert.Equal(1, test.Source.Captures); Assert.False(test.Surface.IsLiveSelection); Assert.NotNull(test.Surface.Image);
        test.Source.Value = 201;
        test.Window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        await running.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, test.Source.Captures); Assert.Equal(87, test.Copied!.Bgra32[0]);
    });
    [Fact]
    public Task EyedropperSamplesTheFrozenFrameWithoutReselectingTheRegion()=>ui.Run(async()=>
    {
        using var test=new Harness();var running=test.Start();test.SelectRegion();
        test.Source.Value=123;test.Click("Edit screenshot");
        test.Click("Pick color from screenshot");
        test.Window.MouseDown(new Point(120,170),MouseButton.Left);
        test.Window.MouseUp(new Point(120,170),MouseButton.Left);
        var palette=test.Window.GetLogicalDescendants().OfType<ColorPicker>().Single();
        Assert.Equal(0xff00007bu,palette.Color.ToUInt32());
        test.Window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});
        Assert.NotNull(await running.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(140,test.Copied!.Width);
    });
    [Fact]
    public Task VideoSetupGripMovesTheRegionThatWillBeRecorded()=>ui.Run(async()=>
    {
        RecordingStartRequest? request=null;
        using var test=new Harness((value,_)=>{request=value;return Task.CompletedTask;});
        var running=test.Start();test.SelectRegion();test.Click("Video");test.Window.UpdateLayout();
        var grip=test.Window.GetVisualDescendants().OfType<Border>().Single(c=>Avalonia.Automation.AutomationProperties.GetName(c)=="Move recording region");
        Assert.True(grip.IsVisible);
        var point=grip.TranslatePoint(new Point(grip.Bounds.Width/2,5),test.Window)!.Value;
        test.Window.MouseDown(point,MouseButton.Left);
        test.Window.MouseMove(new Point(point.X+30,point.Y+24));
        test.Window.MouseUp(new Point(point.X+30,point.Y+24),MouseButton.Left);
        test.Click("Record");
        await running.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new ShareVibeX.Core.Capture.PixelRect(130,174,140,80),request!.Region);
    });
    [Fact]
    public Task VideoAndReturnToChoosingStayLiveWithoutCapturing() => ui.Run(async () =>
    {
        using var test = new Harness(); var running = test.Start(); test.SelectRegion();
        test.Click("Video"); Assert.True(test.Surface.IsLiveSelection); Assert.Equal(0, test.Source.Captures);
        test.Click("Back"); Assert.True(test.Surface.IsLiveSelection); Assert.Equal(0, test.Source.Captures);
        test.Window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Null(await running.WaitAsync(TimeSpan.FromSeconds(3))); Assert.Equal(0, test.Source.Captures);
    });
    [Fact]
    public Task EnterBeforeModeChoiceCopiesEvenWhenModeButtonHasKeyboardFocus() => ui.Run(async () =>
    {
        using var test = new Harness(); var running = test.Start(); test.SelectRegion();
        test.Window.GetLogicalDescendants().OfType<Button>().Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Edit screenshot").Focus();
        test.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await running.WaitAsync(TimeSpan.FromSeconds(3)); Assert.NotNull(test.Copied); Assert.Equal(1, test.Source.Captures);
    });
    private sealed class Harness : IDisposable
    {
        private readonly List<Window> windows = [];
        private readonly ClipboardImageOwner clipboard = new();
        private readonly SelectionOverlayController controller;
        public Source Source { get; }
        public CapturedFrame? Copied;
        public Window Window => windows.Single();
        public EditorSurface Surface => Window.GetVisualDescendants().OfType<EditorSurface>().Single();
        public Harness(Func<RecordingStartRequest,CancellationToken,Task>? record=null,bool showActivated=true)
        {
            Source = new Source(windows);
            controller = new SelectionOverlayController(Source, clipboard, new AppSettings(), (_, frame) => { Copied = frame; return Task.CompletedTask; },
                _ => { Assert.All(windows, window => Assert.False(window.IsVisible)); return Task.CompletedTask; },
                () => { var window = new Window(); windows.Add(window); return window; },record:record){ShowActivated=showActivated};
        }
        public Task<CaptureSession?> Start() => controller.SelectAsync(DesktopLayout.FromMonitors(Source.GetMonitors()), default);
        public void SelectRegion()
        {
            Window.MouseDown(new Point(100, 150), MouseButton.Left); Window.MouseMove(new Point(240, 230)); Window.MouseUp(new Point(240, 230), MouseButton.Left);
        }
        public void Click(string text) => Window.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, text) || Avalonia.Automation.AutomationProperties.GetName(button) == text)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Dispose() { foreach (var window in windows) window.Close(); clipboard.Dispose(); }
    }
    private sealed class Source(List<Window> windows) : IDesktopCapture
    {
        public int Captures; public byte Value; public bool AllHidden;
        public IReadOnlyList<MonitorInfo> GetMonitors() => [new("screen", new(0, 0, 800, 600), 1)];
        public Task<DesktopSnapshot> CaptureAsync(CancellationToken token = default)
        {
            Captures++; AllHidden = windows.All(window => !window.IsVisible);
            var pixels = new byte[800 * 600 * 4];
            for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = Value; pixels[i + 3] = 255; }
            return Task.FromResult(new DesktopSnapshot(new(0, 0, 800, 600), GetMonitors(), new(800, 600, 3200, pixels)));
        }
    }
}
