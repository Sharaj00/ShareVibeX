using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class CompactEditorControlTests(UiTestFixture ui)
{
    [Fact]
    public Task WheelOverNumberChangesThicknessWithoutSpinnerButtons()=>ui.Run(()=>
    {
        var state=new CaptureEditorState();var number=new WheelThickness(state,()=>{});var window=new Window{Width=150,Height=90,Content=number};
        try{window.Show();window.MouseWheel(new(30,30),new Vector(0,2));Assert.Equal(5,state.Width);}
        finally{window.Close();}
    });
    [Fact]
    public void CtrlShiftZRedoesInsteadOfUndoingAgain()
    {
        var state=new CaptureEditorState();state.Document.Add(new(Guid.NewGuid(),AnnotationKind.Rectangle,new(0,0,20,20),[]));
        Assert.True(EditorKeyboard.HandleHistory(state,Key.Z,KeyModifiers.Control));Assert.Empty(state.Document.Items);
        Assert.True(EditorKeyboard.HandleHistory(state,Key.Z,KeyModifiers.Control|KeyModifiers.Shift));Assert.Single(state.Document.Items);
    }
}
