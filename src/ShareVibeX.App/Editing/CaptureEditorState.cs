using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.App.Editing;

public sealed class CaptureEditorState
{
    public AnnotationDocument Document { get; } = new();
    public PixelRect Region { get; set; }
    public PixelRect? HoverRegion {get;set;}
    public EditorTool Tool { get; set; } = EditorTool.Region;
    public ShapeEffect Effect { get; set; }
    public uint Color { get; set; } = 0xFFFF5050;
    public int Width { get; set; } = 3;
    public int FontSize { get; set; } = 24;
    public string Text { get; set; } = "";
    public Guid? SelectedId { get; set; }
    public Annotation? Draft { get; set; }
    public bool IsExporting { get; set; }
    public bool IsDrawing { get; set; }
    public PixelPoint? CursorPosition { get; set; }
    public bool IsEditingText { get; set; }
    public Guid? EditingTextId { get; set; }
    public bool MoveModifier { get; set; }
    private double wheelRemainder;
    public bool AdjustThickness(double delta)
    {
        wheelRemainder+=delta;var ticks=(int)Math.Truncate(wheelRemainder);wheelRemainder-=ticks;
        var next=Math.Clamp(Width+ticks,1,80);if(next==Width)return false;Width=next;
        if(IsDrawing && Draft is {} draft)Draft=draft with {Width=Width};
        else if(!IsEditingText && Selected is {} selected && selected.Width!=Width)Document.Replace(selected with {Width=Width});
        return true;
    }
    public void SetColor(uint color)
    {
        Color=color;if(!IsDrawing && !IsEditingText && Selected is {} selected && selected.Color!=color)Document.Replace(selected with {Color=color});
    }
    public void SetEffect(ShapeEffect effect)
    {
        Effect=effect;if(!IsDrawing && !IsEditingText && Selected is {Kind:AnnotationKind.Rectangle or AnnotationKind.Ellipse} selected && selected.Effect!=effect)Document.Replace(selected with {Effect=effect});
    }
    public IReadOnlyList<Annotation> PreviewItems()
    {
        var items=Document.Items.ToList();
        if(EditingTextId is {} editingId)items.RemoveAll(item=>item.Id==editingId);
        if(Draft is {} draft){var index=items.FindIndex(item=>item.Id==draft.Id);if(index>=0)items[index]=draft;else items.Add(draft);}
        return items;
    }
    public Annotation? Selected => Document.Items.FirstOrDefault(item=>item.Id==SelectedId);
    public void LoadStyle(Annotation annotation)
    {
        Color=annotation.Color;Width=annotation.Width;Effect=annotation.Effect;Text=annotation.Text;FontSize=annotation.FontSize;
    }
    public Annotation NewAnnotation(IReadOnlyList<PixelPoint> points,EditorTool? gestureTool=null)
    {
        var tool=gestureTool??Tool;
        var bounds=PixelRect.FromPoints(points[0],points[^1]);
        if(tool==EditorTool.Pen) bounds=new(points.Min(p=>p.X),points.Min(p=>p.Y),Math.Max(1,points.Max(p=>p.X)-points.Min(p=>p.X)),Math.Max(1,points.Max(p=>p.Y)-points.Min(p=>p.Y)));
        if(tool==EditorTool.Text && (bounds.Width<32 || bounds.Height<24))bounds=new(points[0].X,points[0].Y,240,72);
        return new(Guid.NewGuid(),Enum.Parse<AnnotationKind>(tool.ToString()),bounds,points.ToArray(),Color,Width,Effect,tool==EditorTool.Text?"":Text,FontSize);
    }
}
