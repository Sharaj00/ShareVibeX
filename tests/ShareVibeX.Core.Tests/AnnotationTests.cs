using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.Core.Tests;

public sealed class AnnotationTests
{
    private static Annotation Shape() => new(Guid.NewGuid(),AnnotationKind.Arrow,new(-20,5,30,20),[new(-20,5),new(10,25)]);
    [Fact]
    public void UndoRedoRestoresEditsAndDeletion()
    {
        var document = new AnnotationDocument(); var original=Shape(); document.Add(original);
        var moved=original.Move(10,-5); document.Replace(moved);
        Assert.Equal(new PixelPoint(-10,0),document.Items[0].Points[0]);
        document.Undo(); Assert.Equal(original,document.Items[0]);
        document.Redo(); Assert.Equal(moved,document.Items[0]);
        document.Delete(original.Id); Assert.Empty(document.Items);
        document.Undo(); Assert.Equal(moved,document.Items[0]);
    }
    [Fact]
    public void NewEditAfterUndoDiscardsRedoBranch()
    {
        var document=new AnnotationDocument();document.Add(Shape());document.Undo();
        var replacement=Shape();document.Add(replacement);document.Redo();
        Assert.Single(document.Items);Assert.Equal(replacement,document.Items[0]);
        document.Clear();Assert.Empty(document.Items);document.Undo();Assert.Equal(replacement,document.Items[0]);
    }
}
