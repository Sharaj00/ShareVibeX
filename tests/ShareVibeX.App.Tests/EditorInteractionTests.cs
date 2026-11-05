using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class EditorInteractionTests(UiTestFixture ui)
{
    [Fact]
    public Task ReleaseKeepsSelectionAndAllowsDrawingOnSameSurface()=>ui.Run(()=>
    {
        var state=new CaptureEditorState();var monitor=new MonitorInfo("test",new(0,0,400,300),1);
        var source=new DesktopSnapshot(monitor.Bounds,[monitor],new(400,300,1600,new byte[480000]));
        using var bitmap=ShareVibeX.App.Capture.BitmapExportService.ToBitmap(source.Frame);
        var surface=new EditorSurface(state,source,monitor){Image=bitmap};
        var window=new Window {Width=400,Height=300,WindowDecorations=WindowDecorations.None,Content=surface};
        try
        {
            window.Show();window.MouseDown(new(30,30),MouseButton.Left);window.MouseMove(new(250,180));window.MouseUp(new(250,180),MouseButton.Left);
            Assert.Equal(220,state.Region.Width);Assert.Equal(150,state.Region.Height);Assert.True(window.IsVisible);
            var original=state.Region;state.Tool=EditorTool.Rectangle;
            window.MouseDown(new(70,70),MouseButton.Left);window.MouseMove(new(160,130));window.MouseUp(new(160,130),MouseButton.Left);
            Assert.Single(state.Document.Items);Assert.Equal(original,state.Region);Assert.True(window.IsVisible);
            state.Document.Undo();Assert.Empty(state.Document.Items);
        }
        finally{window.Close();}
    });
    [Fact]
    public Task ExistingRegionCanMoveAndResizeWithoutEndingCapture()=>ui.Run(()=>
    {
        var state=new CaptureEditorState{Region=new(40,40,100,80)};var monitor=new MonitorInfo("test",new(0,0,400,300),1);
        var source=new DesktopSnapshot(monitor.Bounds,[monitor],new(400,300,1600,new byte[480000]));
        using var bitmap=ShareVibeX.App.Capture.BitmapExportService.ToBitmap(source.Frame);
        var surface=new EditorSurface(state,source,monitor){Image=bitmap};var window=new Window{Width=400,Height=300,WindowDecorations=WindowDecorations.None,Content=surface};
        try
        {
            window.Show();var origin=window.PointToScreen(new Point(0,0));state.Region=new(origin.X+40,origin.Y+40,100,80);
            window.MouseDown(new(80,70),MouseButton.Left);window.MouseMove(new(110,100));window.MouseUp(new(110,100),MouseButton.Left);
            Assert.Equal(origin.X+70,state.Region.X);Assert.Equal(origin.Y+70,state.Region.Y);
            window.MouseDown(new(170,150),MouseButton.Left);window.MouseMove(new(200,180));window.MouseUp(new(200,180),MouseButton.Left);
            Assert.Equal(130,state.Region.Width);Assert.Equal(110,state.Region.Height);Assert.True(window.IsVisible);
        }
        finally{window.Close();}
    });
}
