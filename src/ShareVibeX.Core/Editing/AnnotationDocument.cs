using ShareVibeX.Core.Capture;

namespace ShareVibeX.Core.Editing;

public enum AnnotationKind { Rectangle, Ellipse, Pen, Line, Arrow, Text }
public enum ShapeEffect { Outline, Fill, Blur }
public sealed record Annotation(Guid Id, AnnotationKind Kind, PixelRect Bounds, IReadOnlyList<PixelPoint> Points,
    uint Color = 0xFFFF5050, int Width = 3, ShapeEffect Effect = ShapeEffect.Outline, string Text = "Text", int FontSize = 24)
{
    public Annotation Move(int x, int y) => this with { Bounds = Bounds with { X = Bounds.X+x,Y = Bounds.Y+y }, Points = Points.Select(p => new PixelPoint(p.X+x,p.Y+y)).ToArray() };
}

public sealed class AnnotationDocument
{
    private Annotation[] items = [];
    private readonly Stack<Annotation[]> undo = new(), redo = new();
    public IReadOnlyList<Annotation> Items => Array.AsReadOnly(items);
    public void Add(Annotation item) => Commit([..items,item]);
    public void Replace(Annotation item)
    {
        if (items.Any(value => value.Id==item.Id)) Commit(items.Select(value => value.Id==item.Id ? item : value).ToArray());
    }
    public void Delete(Guid id) { if(items.Any(value=>value.Id==id)) Commit(items.Where(value=>value.Id!=id).ToArray()); }
    public void Undo() { if(undo.TryPop(out var value)){redo.Push(items);items=value;} }
    public void Redo() { if(redo.TryPop(out var value)){undo.Push(items);items=value;} }
    public void Clear() { if(items.Length>0) Commit([]); }
    private void Commit(Annotation[] next){undo.Push(items);items=next;redo.Clear();}
}
