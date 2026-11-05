using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows.Capture;

namespace ShareVibeX.App.Tests;

public sealed class WgcCaptureLifetimeTests
{
    private static readonly MonitorInfo Monitor=new("test-monitor",new(-2,0,2,2),1.25);
    private static WgcMonitorFrame Frame()=>new(Monitor,new(2,2,8,Enumerable.Repeat((byte)255,16).ToArray()),TimeSpan.FromSeconds(10));
    private static WgcMonitorCapture Capture(Session session,TimeSpan? timeout=null)=>new(_=>session,monitor=>new(monitor,1),timeout);

    [Fact] public async Task FreshFrameDisablesCursorAndClosesExactlyOnce()
    {
        var session=new Session{SignalOnStart=true};var result=await Capture(session).CaptureAsync(Monitor);
        Assert.Equal(Monitor,result.Monitor);Assert.Equal(Frame().Timestamp,result.Timestamp);Assert.Equal(Frame().Frame.Bgra32,result.Frame.Bgra32);Assert.False(session.Cursor);Assert.Equal(1,session.Reads);Assert.Equal(1,session.Disposals);
        session.RaiseLate();Assert.Equal(1,session.Reads);
    }
    [Fact] public async Task NoFrameTimesOutAndDisposesSession()
    {
        var session=new Session();await Assert.ThrowsAsync<CaptureSourceUnavailableException>(()=>Capture(session,TimeSpan.FromMilliseconds(30)).CaptureAsync(Monitor));
        Assert.Equal(1,session.Disposals);
    }
    [Fact] public async Task CancellationWhileWaitingDoesNotBecomeSourceFailure()
    {
        var session=new Session();using var cancellation=new CancellationTokenSource();var task=Capture(session).CaptureAsync(Monitor,cancellation.Token);
        await session.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);
        Assert.Equal(1,session.Disposals);session.RaiseLate();Assert.Equal(0,session.Reads);
    }
    [Fact] public async Task PreCancellationNeverResolvesOrCreatesNativeObjects()
    {
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        var capture=new WgcMonitorCapture(_=>throw new Exception("Factory must not run"),_=>throw new Exception("Resolver must not run"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>capture.CaptureAsync(Monitor,cancellation.Token));
    }
    [Fact] public async Task StartFailureStillDisposesSession()
    {
        var failure=new IOException("Synthetic startup failure");var session=new Session{StartError=failure};
        Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>Capture(session).CaptureAsync(Monitor)));Assert.Equal(1,session.Disposals);
    }
    [Fact] public async Task CancellationDuringReadbackDrainsReaderBeforeDisposal()
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var exited=false;
        var session=new Session{SignalAsynchronously=true,Read=token=>
        {entered.TrySetResult();try{token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();return Frame();}finally{exited=true;}}};
        session.OnDispose=()=>Assert.True(exited);
        using var cancellation=new CancellationTokenSource();var task=Capture(session).CaptureAsync(Monitor,cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);
        Assert.Equal(1,session.Disposals);
    }
    [Fact] public async Task WrongGeometryIsNotEligibleForFallback()
    {
        var session=new Session{SignalOnStart=true,Read=_=>Frame() with{Monitor=Monitor with{Bounds=new(0,0,3,3)}}};
        await Assert.ThrowsAsync<InvalidDataException>(()=>Capture(session).CaptureAsync(Monitor));Assert.Equal(1,session.Disposals);
    }
    [Fact] public async Task EmptyNotificationWaitsForTheNextFrame()
    {
        var session=new Session{SignalOnStart=true};var reads=0;
        session.Read=_=>Interlocked.Increment(ref reads)==1?null:Frame();
        var task=Capture(session).CaptureAsync(Monitor);
        await session.StartReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));session.RaiseLate();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Monitor,result.Monitor);Assert.Equal(2,session.Reads);Assert.Equal(1,session.Disposals);
    }
    [Fact] public async Task CleanupFailurePreservesTheOriginalStartError()
    {
        var failure=new IOException("Synthetic startup failure");
        var session=new Session{StartError=failure,OnDispose=()=>throw new IOException("Synthetic cleanup failure")};
        Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>Capture(session).CaptureAsync(Monitor)));
    }
    [Fact] public async Task FrameAndCancellationRaceNeverPublishesAfterCleanup()
    {
        for(var i=0;i<20;i++)
        {
            var session=new Session{SignalAsynchronously=true};using var cancellation=new CancellationTokenSource();
            var task=Capture(session).CaptureAsync(Monitor,cancellation.Token);await session.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));cancellation.Cancel();
            try{await task;}catch(OperationCanceledException){}
            Assert.Equal(1,session.Disposals);var reads=session.Reads;session.RaiseLate();Assert.Equal(reads,session.Reads);
        }
    }
    private sealed class Session:IWgcMonitorSession
    {
        public event Action? FrameArrived;private Action? retained;
        public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource StartReturned=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cursor=true,SignalOnStart,SignalAsynchronously;public int Reads,Disposals;public Exception? StartError;
        public Func<CancellationToken,WgcMonitorFrame?> Read=_=>Frame();public Action? OnDispose;
        public void Start(bool captureCursor)
        {
            Cursor=captureCursor;retained=FrameArrived;Started.TrySetResult();if(StartError is {} error)throw error;
            if(SignalOnStart)FrameArrived?.Invoke();if(SignalAsynchronously)_=Task.Run(()=>retained?.Invoke());
            StartReturned.TrySetResult();
        }
        public WgcMonitorFrame? TryReadFrame(CancellationToken token){Assert.Equal(0,Disposals);Interlocked.Increment(ref Reads);return Read(token);}
        public void RaiseLate()=>retained?.Invoke();
        public void Dispose(){OnDispose?.Invoke();Interlocked.Increment(ref Disposals);}
    }
}
