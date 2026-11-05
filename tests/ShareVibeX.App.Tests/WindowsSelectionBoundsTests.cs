using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Tests;

public sealed class WindowsSelectionBoundsTests
{
    [Theory]
    [InlineData(-1280,50,true)] [InlineData(-1,719,true)] [InlineData(0,719,false)] [InlineData(-1281,50,false)] [InlineData(-100,720,false)]
    public void BoundsUsePhysicalExclusiveEdgesAcrossNegativeMonitors(int x,int y,bool expected)=>Assert.Equal(expected,WindowsSelectionTools.Contains(new(-1280,50,1280,670),new(x,y)));
    [Fact] public void EmptyBoundsNeverContainCursor()=>Assert.False(WindowsSelectionTools.Contains(default,new(0,0)));
}
