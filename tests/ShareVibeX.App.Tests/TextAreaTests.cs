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
public sealed class TextAreaTests(UiTestFixture ui)
{
    [Fact]
    public void ExistingTextIsNotPaintedBehindItsActiveInputBox()
    {
        var state=new CaptureEditorState();var text=new Annotation(Guid.NewGuid(),AnnotationKind.Text,new(10,10,160,60),[],Text:"Hello");
        state.Document.Add(text);state.EditingTextId=text.Id;Assert.Empty(state.PreviewItems());
        state.EditingTextId=null;Assert.Single(state.PreviewItems());
    }
    [Fact]
    public Task ResizingLiveInputScalesEnteredTextAndCommitsItsBounds()=>ui.Run(()=>
    {
        Annotation? result=null;var editor=new InlineTextEditor(new(Guid.NewGuid(),AnnotationKind.Text,new(10,10,120,60),[],Text:""),new(0,0,500,400),1,annotation=>result=annotation,()=>{});
        Assert.Equal("",editor.Input.Text);editor.Input.Text="Hello";editor.ResizeTo(new(10,10,120,60));var small=editor.Input.FontSize;
        editor.ResizeTo(new(10,10,240,120));Assert.True(editor.Input.FontSize>small*1.8);
        editor.Commit();Assert.Equal("Hello",result!.Text);Assert.Equal(new ShareVibeX.Core.Capture.PixelRect(10,10,240,120),result.Bounds);
    });
    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public Task LiveTextInsetsUseTheSamePhysicalPixelsAsExport(double scale)=>ui.Run(()=>
    {
        var editor=new InlineTextEditor(new(Guid.NewGuid(),AnnotationKind.Text,new(10,10,180,60),[],Text:"Width limited text"),new(0,0,800,600),scale,_=>{},()=>{});
        Assert.Equal(FittedTextLayout.Padding,(editor.BorderThickness.Left+editor.Input.Padding.Left)*scale,4);
        Assert.Equal(FittedTextLayout.Padding,(editor.BorderThickness.Top+editor.Input.Padding.Top)*scale,4);
    });
    [Fact]
    public Task ReopeningSeamTextPreservesItsOriginalBounds()=>ui.Run(()=>
    {
        var original=new Annotation(Guid.NewGuid(),AnnotationKind.Text,new(1800,100,240,60),[],Text:"Across the seam");
        Annotation? committed=null;
        var editor=new InlineTextEditor(original,new(0,0,1920,1080),1,result=>committed=result,()=>{},new(0,0,3840,1080));
        Assert.Equal(original.Bounds,editor.Area);editor.Commit();Assert.Equal(original.Bounds,committed!.Bounds);
    });
    [Fact]
    public Task ExportRendersShapedTextInItsBoxWithCorrectColor()=>ui.Run(()=>
    {
        var pixels=new byte[200*100*4];for(var index=3;index<pixels.Length;index+=4)pixels[index]=255;
        var snapshot=new DesktopSnapshot(new(0,0,200,100),[],new(200,100,800,pixels));
        var text=new Annotation(Guid.NewGuid(),AnnotationKind.Text,new(20,20,120,60),[],0xFFFF0000,Text:"Hello\nمرحبا");
        var frame=AnnotationRenderer.Render(snapshot,[text]);var visible=0;
        for(var y=0;y<100;y++)for(var x=0;x<200;x++)
        {
            var offset=(y*200+x)*4;
            if(frame.Bgra32[offset+2]>0){visible++;Assert.InRange(x,20,139);Assert.InRange(y,20,79);}
            Assert.Equal(0,frame.Bgra32[offset]);Assert.Equal(0,frame.Bgra32[offset+1]);Assert.Equal(255,frame.Bgra32[offset+3]);
        }
        Assert.True(visible>30);
    });
    [Fact]
    public Task CtrlEnterCommitsFocusedTextInput()=>ui.Run(()=>
    {
        Annotation? result=null;var editor=new InlineTextEditor(new(Guid.NewGuid(),AnnotationKind.Text,new(10,10,180,60),[]),new(0,0,400,300),1,annotation=>result=annotation,()=>{});
        var canvas=new Canvas();canvas.Children.Add(editor);var window=new Window{Width=400,Height=300,Content=canvas};
        try
        {
            window.Show();editor.Input.Text="Typed text";editor.Input.Focus();window.KeyPress(Key.Enter,RawInputModifiers.Control,PhysicalKey.Enter,null);
            Assert.NotNull(result);Assert.Equal("Typed text",result.Text);
        }
        finally{editor.Cancel();window.Close();}
    });
    [Fact]
    public Task EnlargingTextBoxIncreasesFittedFont()=>ui.Run(()=>
    {
        var small=FittedTextLayout.FontSize("Hello\nWorld",new(0,0,120,60));var large=FittedTextLayout.FontSize("Hello\nWorld",new(0,0,240,120));
        Assert.True(large>small*1.8f);Assert.InRange(FittedTextLayout.FontSize(new string('W',80),new(0,0,120,60)),1,small);
    });
    [Fact]
    public Task TextDragRequestsAnEmptyResizableAreaInsteadOfPresetText()=>ui.Run(()=>
    {
        var state=new CaptureEditorState{Region=new(0,0,400,300),Tool=EditorTool.Text};var monitor=new MonitorInfo("test",state.Region,1);
        var snapshot=new DesktopSnapshot(monitor.Bounds,[monitor],new(400,300,1600,new byte[480000]));
        using var image=BitmapExportService.ToBitmap(snapshot.Frame);Annotation? requested=null;
        var surface=new EditorSurface(state,snapshot,monitor){Image=image,TextEditRequested=annotation=>requested=annotation};
        var window=new Window{Width=400,Height=300,WindowDecorations=WindowDecorations.None,Content=surface};
        try
        {
            window.Show();window.MouseDown(new(40,40),MouseButton.Left);window.MouseMove(new(200,110));window.MouseUp(new(200,110),MouseButton.Left);
            Assert.NotNull(requested);Assert.Equal("",requested.Text);Assert.Equal(160,requested.Bounds.Width);Assert.Equal(70,requested.Bounds.Height);Assert.Empty(state.Document.Items);
        }
        finally{window.Close();}
    });
}
