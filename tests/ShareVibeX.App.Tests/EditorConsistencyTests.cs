using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using ShareVibeX.App.Editing;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class EditorConsistencyTests(UiTestFixture ui)
{
    [Fact]
    public Task SwitchingMonitorToolbarsKeepsDisplayedOptionsConsistent()=>ui.Run(()=>
    {
        var state=new CaptureEditorState();
        var first=new EditorToolbar(state,()=>{},_=>Task.CompletedTask,()=>{});
        var second=new EditorToolbar(state,()=>{},_=>Task.CompletedTask,()=>{});
        state.Color=0xFF55CCFF;state.Width=10;state.Effect=ShapeEffect.Blur;
        first.Update(state);second.Update(state);
        Assert.Equal(Color.FromUInt32(0xFF55CCFF),second.GetLogicalDescendants().OfType<ColorPicker>().Single().Color);
        Assert.Contains(second.GetLogicalDescendants().OfType<WheelThickness>(),number=>Avalonia.Automation.AutomationProperties.GetName(number)?.EndsWith(": 10")==true);
        Assert.Equal(0xFF55CCFFu,state.Color);
    });
    [Fact]
    public Task ExportDisablesEveryToolbarUntilCompletion()=>ui.Run(()=>
    {
        var state=new CaptureEditorState();var toolbar=new EditorToolbar(state,()=>{},_=>Task.CompletedTask,()=>{});
        state.IsExporting=true;toolbar.Update(state);Assert.False(toolbar.IsEnabled);
        state.IsExporting=false;toolbar.Update(state);Assert.True(toolbar.IsEnabled);
    });
    [Fact]
    public void MovedDraftKeepsItsOriginalStackingOrder()
    {
        var state=new CaptureEditorState();var lower=new Annotation(Guid.NewGuid(),AnnotationKind.Rectangle,new(0,0,40,40),[]);
        var upper=lower with {Id=Guid.NewGuid()};state.Document.Add(lower);state.Document.Add(upper);state.Draft=lower.Move(10,10);
        Assert.Equal(lower.Id,state.PreviewItems()[0].Id);Assert.Equal(upper.Id,state.PreviewItems()[1].Id);
        Assert.Equal(10,state.PreviewItems()[0].Bounds.X);
    }
}
