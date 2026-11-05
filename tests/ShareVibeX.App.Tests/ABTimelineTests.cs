using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using ShareVibeX.App.Playback;

namespace ShareVibeX.App.Tests;
[Collection("Avalonia UI")]
public sealed class ABTimelineTests(UiTestFixture ui)
{
    [Theory]
    [InlineData(0,0,20,0,20)]
    [InlineData(100,100,80,80,100)]
    [InlineData(20,22,40,20,40)]
    public Task CollapsedAndNearbyHandlesRemainIndependentlyDraggable(long a,long b,long destination,long expectedA,long expectedB)=>ui.Run(()=>
    {
        var strip=new ABRangeStrip();strip.Reset(100);strip.SetRange(a,b);var window=new Window{Width=400,Height=34,Content=strip};window.Show();window.UpdateLayout();
        try
        {
            double X(long frame)=>6+388*frame/100d;
            window.MouseDown(new Point(X(b),8),MouseButton.Left);window.MouseMove(new Point(X(destination),8));window.MouseUp(new Point(X(destination),8),MouseButton.Left);
            Assert.Equal(expectedA,strip.A);Assert.Equal(expectedB,strip.B);
        }
        finally{window.Close();}
    });
    [Fact]
    public Task MarkerAndSeekGesturesAreIndependent()=>ui.Run(()=>
    {
        var strip=new ABRangeStrip();strip.Reset(100);strip.SetRange(20,80);long? sought=null;strip.SeekRequested+=frame=>sought=frame;
        var window=new Window{Width=400,Height=34,Content=strip};window.Show();window.UpdateLayout();
        try
        {
            window.MouseDown(new Point(84,8),MouseButton.Left);window.MouseMove(new Point(123,8));window.MouseUp(new Point(123,8),MouseButton.Left);
            Assert.InRange(strip.A,29,31);Assert.Equal(80,strip.B);Assert.Null(sought);
            window.MouseDown(new Point(200,24),MouseButton.Left);window.MouseMove(new Point(240,24));window.MouseUp(new Point(240,24),MouseButton.Left);
            Assert.InRange(sought!.Value,59,61);Assert.InRange(strip.A,29,31);Assert.Equal(80,strip.B);Assert.False(strip.IsDragging);
        }
        finally{window.Close();}
    });
}
