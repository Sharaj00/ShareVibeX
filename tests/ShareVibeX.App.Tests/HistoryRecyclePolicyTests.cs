using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Tests;

public sealed class HistoryRecyclePolicyTests
{
    [Fact]
    public void PermanentDeletionIsVetoedBeforeShellOperation()
    {
        if(!OperatingSystem.IsWindows())return;
        var sink=new WindowsHistoryFileActions.RecycleSink();
        Assert.True(sink.PreDeleteItem(0,0)<0);Assert.True(sink.PermanentDeleteRejected);Assert.False(sink.Recycled);
    }
    [Theory]
    [InlineData(0,1,true)]
    [InlineData(0,0,false)]
    [InlineData(0x270005,1,false)]
    [InlineData(unchecked((int)0x80070005),0,false)]
    public void OnlyConfirmedRecycleItemCountsAsSuccess(int result,int created,bool expected)
    {
        if(!OperatingSystem.IsWindows())return;
        var sink=new WindowsHistoryFileActions.RecycleSink();
        Assert.Equal(0,sink.PreDeleteItem(0x80,0));Assert.False(sink.PermanentDeleteRejected);
        sink.PostDeleteItem(0x80,0,result,created);
        Assert.Equal(expected,sink.Recycled);Assert.Equal(result,sink.Error);
    }
}
