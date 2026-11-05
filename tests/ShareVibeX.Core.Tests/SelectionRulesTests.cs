using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Tests;

public sealed class SelectionRulesTests
{
    [Theory]
    [InlineData(900,700,100,100)]
    [InlineData(100,100,900,700)]
    public void AltPresetsKeepAnchorAndPhysicalDimensions(int x,int y,int toX,int toY)
    {
        var result=SelectionRules.Drag(new(x,y),new(toX,toY),new(0,0,1000,800),false,true,"640x480");
        const int width=640,height=480;Assert.Equal(width,result.Width);Assert.Equal(height,result.Height);
        Assert.True(result.X>=0&&result.Y>=0&&result.Right<=1000&&result.Bottom<=800);
        Assert.Equal(Math.Min(x,x+(toX<x?-width:width)),result.X);Assert.Equal(Math.Min(y,y+(toY<y?-height:height)),result.Y);
    }
    [Fact]
    public void ShiftSquareOverridesAltAndDoesNotSnap()
    {
        Assert.Equal(new PixelRect(10,20,60,60),SelectionRules.Drag(new(10,20),new(70,150),new(0,0,1000,800),true,true,"640x480"));
    }
    [Fact]
    public void ActiveNegativeMonitorConstrainsMoveAndCreation()
    {
        var bounds=new PixelRect(-1920,-100,1920,1080);
        Assert.Equal(new PixelRect(-200,780,200,200),SelectionRules.Move(new(-300,100,200,200),1000,1000,bounds));
        var result=SelectionRules.Drag(new(-1800,0),new(600,1200),bounds,false,false,"");Assert.Equal(0,result.Right);Assert.Equal(980,result.Bottom);
    }
    [Fact]
    public void TemplatesAndSnapSizesAreValidated()
    {
        Assert.Throws<ArgumentException>(()=>SelectionRules.Validate(new(){InfoTemplate="{unsupported}"}));
        Assert.Throws<ArgumentException>(()=>SelectionRules.Validate(new(){SnapSizes="0x300"}));
        Assert.Throws<ArgumentException>(()=>SelectionRules.Validate(new(){EditorDimPercent=91}));
        Assert.Equal(2,SelectionRules.ParseSnapSizes("640x480;1280x720;640x480").Count);
        Assert.Equal("X -500\n200 × 100",SelectionRules.FormatInfo(new(){InfoTemplate="X {x}{newline}{width} × {height}"},new(-500,0,200,100)));
        Assert.Equal("",SelectionRules.FormatInfo(new(){ShowSize=false},new(0,0,200,100)));
    }
}
