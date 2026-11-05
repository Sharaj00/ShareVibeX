using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.Input;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using PixelRect=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Tests;

public sealed class RecordingPlacementTests
{
    [Theory]
    [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void RightEdgeAndNegativeMonitorStayWithinPhysicalBounds(double scale)
    {
        var monitor=new MonitorInfo("left",new(-1920,0,1920,1080),scale);var region=new PixelRect(-528,0,528,599);
        var position=RecordingControlPlacement.TryPlace(region,monitor,new(380,128),false);
        Assert.NotNull(position);
        var panel=new PixelRect(position.Value.X,position.Value.Y,(int)Math.Ceiling(380*scale),(int)Math.Ceiling(128*scale));
        Assert.Equal(panel,panel.Intersect(monitor.Bounds));Assert.True(panel.Intersect(region).IsEmpty);
    }
    [Fact]
    public void FullMonitorRequiresSuccessfulExclusion()
    {
        var monitor=new MonitorInfo("screen",new(0,0,800,600),1);
        Assert.Null(RecordingControlPlacement.TryPlace(monitor.Bounds,monitor,new(380,128),false));
        Assert.NotNull(RecordingControlPlacement.TryPlace(monitor.Bounds,monitor,new(380,128),true));
    }
    [Fact]
    public void BottomEdgeAndNarrowRegionCanUseSafeSpaceAbove()
    {
        var monitor=new MonitorInfo("screen",new(0,0,800,600),1);
        var point=RecordingControlPlacement.TryPlace(new(720,500,80,100),monitor,new(380,128),false);
        Assert.NotNull(point);Assert.True(point.Value.Y+128<=500);Assert.InRange(point.Value.X,0,420);
    }
}

[Collection("Avalonia UI")]
public sealed class RecordingControlTests(UiTestFixture ui)
{
    [Fact]
    public Task MoveStripTracksPointerUntilRelease()=>ui.Run(()=>
    {
        var theme=new Avalonia.Themes.Fluent.FluentTheme();Avalonia.Application.Current!.Styles.Add(theme);
        var window=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{});
        var region=new PixelRect(100,100,300,200);
        var moves=new List<(PixelRect,int,int)>();
        var monitor=new MonitorInfo("screen",new(0,0,800,600),1);
        window.ConfigureRegionMove(()=>region,(start,x,y)=>{moves.Add((start,x,y));window.FollowRegion(start with{X=start.X+x,Y=start.Y+y},monitor);});
        window.Update(RecordingSessionState.Recording,new(TimeSpan.Zero,0),300,200);
        window.SetRegionMoveAvailable(true);
        try
        {
            Assert.True(window.TryPresent(region,new("screen",new(0,0,800,600),1),_=>true));
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var strip=window.GetLogicalDescendants().OfType<Border>().Single(b=>AutomationProperties.GetName(b)=="Move recording region");
            var start=strip.TranslatePoint(new Point(20,5),window)!.Value;
            var end=start+new Vector(60,10);
            window.MouseDown(start,MouseButton.Left,RawInputModifiers.None);
            window.MouseMove(end,RawInputModifiers.None);
            Assert.Equal((region,60,10),Assert.Single(moves));
            Assert.Equal(new Avalonia.PixelPoint(198,312),window.Position);
            window.MouseUp(end,MouseButton.Left,RawInputModifiers.None);
            window.MouseMove(start,RawInputModifiers.None);
            Assert.Single(moves);
        }
        finally{window.Dismiss();Avalonia.Application.Current!.Styles.Remove(theme);}
    });
    [Fact]
    public Task FailedNativeFrameMaskNeverShowsAnInteriorBlockingWindow()=>ui.Run(()=>
    {
        using var frame=new RecordingRegionFrame();frame.Show(new(100,100,300,200),[new("screen",new(0,0,800,600),1)],_=>true,(_,_,_)=>false);
        Assert.All(frame.Windows,window=>Assert.False(window.IsVisible));
    });
    [Fact]
    public Task RecordingReplacesReadyIconsWithoutChangingTheirAnchorOrSize()=>ui.Run(()=>
    {
        var window=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{},()=>Task.CompletedTask,_=>Task.CompletedTask);
        window.Update(RecordingSessionState.Recording,new(TimeSpan.FromSeconds(2),50000,IsEstimated:true),100,100,canPause:true,canMicrophone:true);
        Assert.True(window.TryPresent(new(10,10,100,100),new("screen",new(0,0,800,600),1),_=>true,new(new(140,180),240,96)));
        Assert.Equal(new Avalonia.PixelPoint(140,180),window.Position);Assert.Equal(240,window.Width);Assert.Equal(96,window.Height);
        Assert.Equal(Avalonia.Media.Brushes.Transparent,window.Background);
        Assert.All(window.GetLogicalDescendants().OfType<Button>().Where(b=>b.IsVisible),b=>{Assert.Equal(40,b.Width);Assert.Equal(40,b.Height);});
        Assert.Contains("≈",string.Join(" ",window.GetLogicalDescendants().OfType<TextBlock>().Select(b=>b.Text)));window.Dismiss();
    });
    [Fact]
    public Task RegionBorderUsesOneHollowSurfaceAndClosesIt()=>ui.Run(()=>
    {
        using var frame=new RecordingRegionFrame();var region=new PixelRect(100,100,300,200);var calls=0;
        frame.Show(region,[new("screen",new(0,0,800,600),1)],_=>{calls++;return true;},(_,w,h)=>w==302&&h==202);
        Assert.Equal(1,calls);var border=Assert.Single(frame.Windows);Assert.Equal(302,border.Width);Assert.Equal(202,border.Height);
        frame.Dispose();Assert.All(frame.Windows,w=>Assert.False(w.IsVisible));
    });
    [Fact]
    public Task ConnectedPauseAndMicUseFourIconsAndFailedMicToggleRollsBack()=>ui.Run(async()=>
    {
        var pauses=0;
        var window=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{},()=>{pauses++;return Task.CompletedTask;},_=>throw new IOException("mic denied"));
        window.Update(RecordingSessionState.Recording,new(TimeSpan.FromSeconds(2),1000),100,100,canPause:true,canMicrophone:true);
        var buttons=window.GetLogicalDescendants().OfType<Button>().Where(b=>b.IsVisible).ToArray();
        Assert.Equal(new[]{"Stop","Pause","Microphone","Cancel recording"},buttons.Select(AutomationProperties.GetName));
        Assert.All(buttons,b=>Assert.IsType<PathIcon>(b.Content));
        buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Equal(1,pauses);
        var mic=Assert.IsType<Avalonia.Controls.Primitives.ToggleButton>(buttons[2]);mic.IsChecked=true;await Task.Yield();Assert.False(mic.IsChecked);
        window.Update(RecordingSessionState.Paused,new(TimeSpan.FromSeconds(2),1000),100,100,canPause:true,canMicrophone:true);
        Assert.Equal("Resume",AutomationProperties.GetName(buttons[1]));window.Dismiss();
    });
    [Fact]
    public Task ExclusionIsAttemptedBeforeWindowBecomesVisible()=>ui.Run(()=>
    {
        var events=new List<string>();var window=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{});
        window.Opened+=(_,_)=>events.Add("show");
        Assert.True(window.TryPresent(new(0,0,800,600),new("screen",new(0,0,800,600),1),_=>{events.Add("exclude");Assert.False(window.IsVisible);return true;}));
        Assert.Equal(new[]{"exclude","show"},events);window.Close();return Task.CompletedTask;
    });
    [Fact]
    public Task FailedExclusionNeverShowsAWindowOverFullCapture()=>ui.Run(()=>
    {
        var window=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{});
        Assert.False(window.TryPresent(new(0,0,800,600),new("screen",new(0,0,800,600),1),_=>false));
        Assert.False(window.IsVisible);window.Close();return Task.CompletedTask;
    });
    [Fact]
    public Task MetricsAndOnlyConnectedActionsAppearAsIcons()=>ui.Run(async()=>
    {
        var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var stops=0;
        var window=new RecordingControlWindow(()=>{stops++;return pending.Task;},()=>Task.CompletedTask,()=>{});
        window.Update(RecordingSessionState.Recording,new(TimeSpan.FromSeconds(2),1000000),102,100);
        var buttons=window.GetLogicalDescendants().OfType<Button>().Where(b=>b.IsVisible).ToArray();
        Assert.Equal(new[]{"Stop","Cancel recording"},buttons.Select(AutomationProperties.GetName));
        Assert.All(buttons,button=>{Assert.IsType<PathIcon>(button.Content);Assert.True(ToolTip.GetShouldUseOverlayLayer(button),"Runtime tooltips must stay inside the capture-excluded control surface.");});
        var text=string.Join(" ",window.GetLogicalDescendants().OfType<TextBlock>().Select(b=>b.Text));
        Assert.Contains("00:00:02",text);Assert.Contains("102 × 100",text);Assert.Contains("4000",text);
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1,stops);pending.SetResult();await Task.Yield();window.Close();
    });
    [Fact]
    public Task UnknownMetricsAreDashesAndClosedWindowRejectsStaleUpdates()=>ui.Run(()=>
    {
        var window=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{});
        window.Update(RecordingSessionState.Starting,RecordingProgress.Empty,0,0);
        Assert.Contains("—",string.Join(" ",window.GetLogicalDescendants().OfType<TextBlock>().Select(b=>b.Text)));
        window.Dismiss();window.Update(RecordingSessionState.Recording,new(TimeSpan.FromSeconds(2),1000),10,10);
        Assert.DoesNotContain("10 × 10",string.Join(" ",window.GetLogicalDescendants().OfType<TextBlock>().Select(b=>b.Text)));
        return Task.CompletedTask;
    });
    [Fact]
    public Task FailedCleanupKeepsCancelAvailableForRetry()=>ui.Run(()=>
    {
        var window=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{});
        window.Update(RecordingSessionState.Failed,RecordingProgress.Empty,10,10,"Retained recording");
        var cancel=window.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Cancel recording");
        Assert.True(cancel.IsVisible);Assert.True(cancel.IsEnabled);window.Dismiss();return Task.CompletedTask;
    });
}


