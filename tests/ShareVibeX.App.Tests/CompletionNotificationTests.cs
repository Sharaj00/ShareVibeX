using ShareVibeX.App.Notifications;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

public sealed class CompletionNotificationTests
{
    [Fact] public void BlockAndExitStopOwnedPlaybackWithoutPlayingAnything()
    {
        var stopped=0;var service=new CompletionNotificationService(()=>new(),sound:()=>throw new Exception("Must not play"),dispatch:a=>a(),stopSound:()=>stopped++);
        service.Block();Assert.Equal(1,stopped);service.Dispose();Assert.Equal(2,stopped);
    }
    [Fact]
    public void DefaultOptionsDoNotQueryDesktopOrPlaySound()
    {
        using var service=new CompletionNotificationService(()=>new(),()=>throw new Exception("Must not query"),()=>throw new Exception("Must not play"),(_,_)=>throw new Exception("Must not show"),a=>a());
        service.Publish(new("Done"));
    }
    [Fact]
    public void BlockClosesNoticeAndDropsQueuedAndBlockedCompletions()
    {
        var actions=new Queue<Action>();var shown=0;var played=0;var closed=0;
        using var service=new CompletionNotificationService(()=>new(){ShowNotifications=true,PlaySound=true},()=>false,()=>played++,(_,_)=>{shown++;return new Disposal(()=>closed++);},a=>actions.Enqueue(a));
        service.Publish(new("First"));actions.Dequeue()();Assert.Equal(1,shown);
        service.Publish(new("Queued"));service.Block();service.Publish(new("Blocked"));service.Unblock();
        while(actions.TryDequeue(out var action))action();
        Assert.Equal(1,shown);Assert.Equal(1,played);Assert.Equal(1,closed);
    }
    [Fact]
    public void FullscreenSuppressionPreventsBothNotificationAndSound()
    {
        var shown=0;var played=0;
        using var service=new CompletionNotificationService(()=>new(){ShowNotifications=true,PlaySound=true},()=>true,()=>played++,(_,_)=>{shown++;return null;},a=>a());
        service.Publish(new("Done"));Assert.Equal(0,shown);Assert.Equal(0,played);
    }
    [Fact]
    public void ReplacementDisposesOldNoticeAndFailuresNeverEscapePublish()
    {
        var closed=0;var played=0;
        using var service=new CompletionNotificationService(()=>new(){ShowNotifications=true,PlaySound=true},()=>false,()=>{played++;throw new IOException("sound failure");},(_,_)=>new Disposal(()=>closed++),a=>a());
        service.Publish(new("First"));service.Publish(new("Second"));Assert.Equal(1,closed);Assert.Equal(2,played);
        service.Block();Assert.Equal(2,closed);
        using var failing=new CompletionNotificationService(()=>throw new IOException("settings failure"),dispatch:a=>a());failing.Publish(new("Done"));
    }
    [Theory]
    [InlineData(CompletionPlacement.BottomRight)] [InlineData(CompletionPlacement.BottomLeft)]
    [InlineData(CompletionPlacement.TopRight)] [InlineData(CompletionPlacement.TopLeft)]
    public void PlacementStaysInsideNegativeCoordinateWorkAreaAtMixedDpi(CompletionPlacement placement)
    {
        var work=new Avalonia.PixelRect(-1280,50,1280,670);
        var result=CompletionToastLayout.Place(work,1.5,new(){Width=2000,Height=2000,Placement=placement});
        Assert.True(result.Position.X>=work.X);Assert.True(result.Position.Y>=work.Y);
        Assert.True(result.Position.X+result.Size.Width*1.5<=work.Right+1);Assert.True(result.Position.Y+result.Size.Height*1.5<=work.Bottom+1);
    }
    [Fact]
    public void FadeIsIncludedInTotalLifetime()
    {
        var options=new CompletionOptions{DurationSeconds=3,FadeSeconds=1};
        Assert.Equal(1,CompletionToastLayout.Opacity(TimeSpan.FromSeconds(1),options));
        Assert.Equal(.5,CompletionToastLayout.Opacity(TimeSpan.FromSeconds(2.5),options));
        Assert.Equal(0,CompletionToastLayout.Opacity(TimeSpan.FromSeconds(3),options));
    }
    private sealed class Disposal(Action action):IDisposable {public void Dispose()=>action();}
}
