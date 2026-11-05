using ShareVibeX.App.Editing;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.App.Tests;

public sealed class AnnotationRendererTests
{
    private static DesktopSnapshot Source()
    {
        var pixels=new byte[20*20*4];
        for(var y=0;y<20;y++) for(var x=0;x<20;x++) {var i=(y*20+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=(byte)((x%2)*255);pixels[i+3]=255;}
        return new(new(-10,-10,20,20),[],new(20,20,80,pixels));
    }
    [Fact]
    public void FilledRectangleIsOpaqueAndSourceRemainsUnchanged()
    {
        var source=Source();var before=source.Frame.Bgra32.ToArray();
        var result=AnnotationRenderer.Render(source,[new(Guid.NewGuid(),AnnotationKind.Rectangle,new(-5,-5,10,10),[],0xFFFF0000,3,ShapeEffect.Fill)]);
        Assert.Equal(new byte[]{0,0,255,255},result.Bgra32.Skip((10*20+10)*4).Take(4));
        Assert.Equal(before,source.Frame.Bgra32);
    }
    [Fact]
    public void EllipseBlurChangesInteriorButNotMaskedCorners()
    {
        var source=Source();var result=AnnotationRenderer.Render(source,[new(Guid.NewGuid(),AnnotationKind.Ellipse,new(-8,-8,16,16),[],0xFFFF0000,4,ShapeEffect.Blur)]);
        Assert.InRange(result.Bgra32[(10*20+10)*4],(byte)30,(byte)225);
        Assert.Equal(source.Frame.Bgra32.Skip((2*20+2)*4).Take(4),result.Bgra32.Skip((2*20+2)*4).Take(4));
    }
    [Fact]
    public void LaterFillCoversEarlierBlur()
    {
        var source=Source();var result=AnnotationRenderer.Render(source,[new(Guid.NewGuid(),AnnotationKind.Rectangle,new(-8,-8,16,16),[],Width:4,Effect:ShapeEffect.Blur),
            new(Guid.NewGuid(),AnnotationKind.Rectangle,new(-2,-2,4,4),[],0xFF00FF00,Effect:ShapeEffect.Fill)]);
        Assert.Equal(new byte[]{0,255,0,255},result.Bgra32.Skip((10*20+10)*4).Take(4));
    }
}
