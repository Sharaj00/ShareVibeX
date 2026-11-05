using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class DirectEditorGestureTests(UiTestFixture ui)
{
    [Fact]
    public Task CtrlDragMovesAnnotationWithoutReplacingChosenTool()=>ui.Run(()=>
    {
        Run((window,state)=>
        {
            state.Tool=EditorTool.Arrow;var shape=new Annotation(Guid.NewGuid(),AnnotationKind.Rectangle,new(40,40,60,40),[]);state.Document.Add(shape);
            window.MouseDown(new(60,60),MouseButton.Left,RawInputModifiers.Control);window.MouseMove(new(90,80),RawInputModifiers.Control);window.MouseUp(new(90,80),MouseButton.Left,RawInputModifiers.Control);
            Assert.Single(state.Document.Items);Assert.Equal(new ShareVibeX.Core.Capture.PixelRect(70,60,60,40),state.Document.Items[0].Bounds);Assert.Equal(EditorTool.Arrow,state.Tool);
        });
    });
    [Fact]
    public Task WheelWhileDrawingChangesDraftAndCommittedBlurStrength()=>ui.Run(()=>
    {
        Run((window,state)=>
        {
            state.Tool=EditorTool.Rectangle;state.Effect=ShapeEffect.Blur;
            window.MouseDown(new(40,40),MouseButton.Left);window.MouseMove(new(100,90));
            window.MouseWheel(new(100,90),new Vector(0,2),RawInputModifiers.LeftMouseButton);
            Assert.Equal(5,state.Width);Assert.Equal(5,state.Draft!.Width);
            window.MouseUp(new(100,90),MouseButton.Left);Assert.Equal(5,state.Document.Items.Single().Width);
        });
    });
    [Fact]
    public Task CtrlClickDoesNotCreateAnEmptyUndoStep()=>ui.Run(()=>
    {
        Run((window,state)=>
        {
            state.Tool=EditorTool.Pen;state.Document.Add(new(Guid.NewGuid(),AnnotationKind.Rectangle,new(40,40,60,40),[]));
            window.MouseDown(new(60,60),MouseButton.Left,RawInputModifiers.Control);window.MouseUp(new(60,60),MouseButton.Left,RawInputModifiers.Control);
            state.Document.Undo();Assert.Empty(state.Document.Items);
        });
    });
    private static void Run(Action<Window,CaptureEditorState> action)
    {
        var state=new CaptureEditorState{Region=new(0,0,400,300)};var monitor=new MonitorInfo("test",state.Region,1);
        var snapshot=new DesktopSnapshot(monitor.Bounds,[monitor],new(400,300,1600,new byte[480000]));
        using var bitmap=BitmapExportService.ToBitmap(snapshot.Frame);var surface=new EditorSurface(state,snapshot,monitor){Image=bitmap};
        var window=new Window{Width=400,Height=300,WindowDecorations=WindowDecorations.None,Content=surface};
        try{window.Show();action(window,state);}finally{window.Close();}
    }
}
