using ShareVibeX.Core.Capture;

namespace ShareVibeX.Core.Tests;

public sealed class PixelRectTests
{
    [Fact]
    public void ReverseDragNormalizesPhysicalBounds() => Assert.Equal(new PixelRect(-100,-50,300,150),
        PixelRect.FromPoints(new(200,100), new(-100,-50)));

    [Fact]
    public void NegativeDesktopOriginMapsToFrame() => Assert.Equal(new PixelRect(0,0,101,51),
        new PixelRect(-1920,0,101,51).ToFrameRect(new(-1920,0,3840,1080)));

    [Fact]
    public void IntersectionExcludesRightAndBottom() => Assert.Equal(new PixelRect(90,90,10,10),
        new PixelRect(90,90,30,30).Intersect(new(0,0,100,100)));

    [Fact]
    public void NonOverlappingIntersectionIsEmpty() => Assert.True(new PixelRect(-100,0,10,10).Intersect(new(0,0,100,100)).IsEmpty);
}

public sealed class CaptureCoordinatorTests
{
    private static DesktopSnapshot Snapshot() => new(new(0,0,100,100), [new("screen", new(0,0,100,100), 1)], new(100,100,400,new byte[40000]));

    [Fact]
    public void ConcurrentSessionIsRejectedUntilEnd()
    {
        var coordinator = new CaptureCoordinator();
        Assert.True(coordinator.TryBeginSelection());
        Assert.False(coordinator.TryBeginSelection());
        coordinator.SetSnapshot(Snapshot());
        coordinator.CompleteSelection(new(2,3,10,20));
        Assert.Equal(CaptureState.Ready, coordinator.State);
        Assert.Equal(new PixelRect(2,3,10,20), coordinator.Session!.Region);
        Assert.Equal(CaptureMode.Screenshot, coordinator.Session.Mode);
        Assert.False(coordinator.TryBeginSelection());
        coordinator.EndSession();
        Assert.Null(coordinator.Session);
        Assert.True(coordinator.TryBeginSelection());
    }

    [Fact]
    public void CompletionRequiresSnapshot()
    {
        var coordinator = new CaptureCoordinator();
        coordinator.TryBeginSelection();
        Assert.Throws<InvalidOperationException>(() => coordinator.CompleteSelection(new(0,0,10,10)));
    }

    [Theory]
    [InlineData(0,0,0,2)]
    [InlineData(-1,0,2,2)]
    [InlineData(99,99,2,2)]
    public void InvalidRegionCannotComplete(int x, int y, int width, int height)
    {
        var coordinator = new CaptureCoordinator();
        coordinator.TryBeginSelection(); coordinator.SetSnapshot(Snapshot());
        Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.CompleteSelection(new(x,y,width,height)));
        Assert.Equal(CaptureState.Selecting, coordinator.State);
    }

    [Fact]
    public void CancelReleasesSnapshotAndPreventsLateCompletion()
    {
        var coordinator = new CaptureCoordinator();
        coordinator.TryBeginSelection(); coordinator.SetSnapshot(Snapshot()); coordinator.Cancel();
        Assert.Equal(CaptureState.Idle, coordinator.State);
        Assert.Null(coordinator.Session);
        Assert.Throws<InvalidOperationException>(() => coordinator.SetSnapshot(Snapshot()));
        Assert.Throws<InvalidOperationException>(() => coordinator.CompleteSelection(new(0,0,10,10)));
    }
}
