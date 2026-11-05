using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class AdvancedSelectionGestureTests(UiTestFixture ui)
{
    [Fact]
    public Task InitialClickDetectsCurrentWindowWithoutPriorMove()=>ui.Run(()=>
    {
        Run(new(){DetectWindows=true},(window,surface,state)=>
        {
            surface.DetectRegion=(_,_)=>new(100,100,250,150);
            window.MouseDown(new(170,170),MouseButton.Left);window.MouseUp(new(170,170),MouseButton.Left);
            Assert.Equal(new PixelRect(100,100,250,150),state.Region);
        });
    });
    [Fact]
    public Task HoverClickSelectsCandidateWhileDraggingRemainsManual()=>ui.Run(()=>
    {
        Run(new(){DetectWindows=true},(window,surface,state)=>
        {
            var calls=0;surface.DetectRegion=(_,_)=>{calls++;return new(100,80,300,200);};
            window.MouseMove(new(200,200));Assert.Equal(new PixelRect(100,80,300,200),state.HoverRegion);
            window.MouseDown(new(200,200),MouseButton.Left);window.MouseUp(new(200,200),MouseButton.Left);
            Assert.Equal(new PixelRect(100,80,300,200),state.Region);Assert.Equal(2,calls);
            state.Region=default;state.HoverRegion=new(100,80,300,200);
            window.MouseDown(new(200,200),MouseButton.Left);window.MouseMove(new(350,350));window.MouseUp(new(350,350),MouseButton.Left);
            Assert.Equal(new PixelRect(200,200,150,150),state.Region);
        });
    });
    [Fact]
    public Task ActiveMonitorBoundsConstrainLiveDrag()=>ui.Run(()=>
    {
        Run(new(){RestrictToActiveMonitor=true},(window,surface,state)=>
        {
            surface.ActiveMonitorBounds=new(100,100,200,150);
            window.MouseDown(new(150,150),MouseButton.Left);window.MouseMove(new(700,500));window.MouseUp(new(700,500),MouseButton.Left);
            Assert.Equal(new PixelRect(150,150,150,100),state.Region);
        });
    });
    [Fact]
    public Task DisabledDetectionDoesNotInvokePlatformCallback()=>ui.Run(()=>
    {
        Run(new(),(window,surface,_)=>{surface.DetectRegion=(_,_)=>throw new Exception("Must not query windows");window.MouseMove(new(80,70));});
    });
    private static void Run(SelectionOptions options,Action<Window,EditorSurface,CaptureEditorState> action)
    {
        var state=new CaptureEditorState();var monitor=new MonitorInfo("test",new(0,0,800,600),1);
        var surface=new EditorSurface(state,new DesktopLayout(monitor.Bounds,[monitor]),monitor){IsLiveSelection=true,Options=options};
        var window=new Window{Width=800,Height=600,WindowDecorations=WindowDecorations.None,Content=surface};surface.Changed=_=>surface.InvalidateVisual();
        try{window.Show();window.UpdateLayout();action(window,surface,state);}finally{window.Close();}
    }
}
