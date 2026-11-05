using ShareVibeX.Platform.Windows;
namespace ShareVibeX.App.Tests;
public sealed class ExclusiveFullscreenTests
{
    [Theory]
    [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,false)] [InlineData(3,true)] [InlineData(4,false)] [InlineData(5,false)] [InlineData(6,false)]
    public void OnlyDirect3dFullscreenSuppressesCompletion(int state,bool expected)=>Assert.Equal(expected,WindowsCompletionPlatform.IsExclusiveFullscreenState(0,state));
    [Fact] public void FailedShellQueryDoesNotSilentlySuppressCompletion()=>Assert.False(WindowsCompletionPlatform.IsExclusiveFullscreenState(unchecked((int)0x80004005),3));
}
