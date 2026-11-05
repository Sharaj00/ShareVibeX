using ShareVibeX.Platform.Windows.Playback;

namespace ShareVibeX.App.Tests;

public sealed class PreviewAudioTests
{
    [Fact]
    public async Task BoundedPcmQueuePreservesOrderAndAppliesBackpressure()
    {
        using var queue=new PreviewPcmQueue(16);var bytes=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();
        var pending=queue.WriteAsync(bytes,default);Assert.False(pending.IsCompleted);Assert.Equal(16,queue.BufferedBytes);
        var first=new byte[16];Assert.Equal(16,queue.Read(first,0,16));await pending.WaitAsync(TimeSpan.FromSeconds(2));
        queue.Complete();var last=new byte[16];Assert.Equal(16,queue.Read(last,0,16));Assert.Equal(bytes[..16],first);Assert.Equal(bytes[16..],last);
        Assert.Equal(0,queue.Read(last,0,16));
    }
    [Fact]
    public async Task QueueCancellationDisposalAndFrameAlignmentAreExplicit()
    {
        using var queue=new PreviewPcmQueue(8);await Assert.ThrowsAsync<ArgumentException>(()=>queue.WriteAsync(new byte[3],default));
        await queue.WriteAsync(new byte[8],default);using var token=new CancellationTokenSource();var pending=queue.WriteAsync(new byte[8],token.Token);token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);queue.Dispose();Assert.Equal(0,queue.Read(new byte[8],0,8));
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>queue.WriteAsync(new byte[8],default));
    }
}
