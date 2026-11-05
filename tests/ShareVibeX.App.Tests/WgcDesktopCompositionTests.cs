using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows.Capture;
using ShareVibeX.App.Capture;

namespace ShareVibeX.App.Tests;

public sealed class WgcDesktopCompositionTests
{
    private static readonly MonitorInfo Left=new("a",new(-2,0,2,2),1.5);
    private static readonly MonitorInfo Right=new("b",new(1,-1,1,3),1);
    private static WgcMonitorFrame Frame(MonitorInfo monitor,byte color,TimeSpan? stamp=null)
    {
        var stride=monitor.Bounds.Width*4+4;var pixels=Enumerable.Repeat(color,stride*monitor.Bounds.Height).ToArray();
        return new(monitor,new(monitor.Bounds.Width,monitor.Bounds.Height,stride,pixels),stamp??TimeSpan.FromSeconds(10));
    }
    [Fact] public void NegativePortraitPaddedRowsAndOpaqueGaps()
    {
        var snapshot=WgcDesktopComposition.Compose([Right,Left],[Frame(Left,10),Frame(Right,20)]);
        Assert.Equal(new PixelRect(-2,-1,4,3),snapshot.Bounds);Assert.Equal(new[]{Left,Right},snapshot.Monitors);
        byte Pixel(int x,int y)=>snapshot.Frame.Bgra32[y*snapshot.Frame.Stride+x*4];
        Assert.Equal(0,Pixel(0,0));Assert.Equal(20,Pixel(3,0));Assert.Equal(10,Pixel(0,1));Assert.Equal(0,Pixel(2,2));
        Assert.All(Enumerable.Range(0,12),i=>Assert.Equal(255,snapshot.Frame.Bgra32[i*4+3]));
    }
    [Fact] public void MissingDuplicateAndInvalidFramesAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>WgcDesktopComposition.Compose([Left,Right],[Frame(Left,10)]));
        Assert.Throws<InvalidDataException>(()=>WgcDesktopComposition.Compose([Left,Right],[Frame(Left,10),Frame(Left,20)]));
        var invalid=Frame(Left,10) with{Frame=new(2,2,8,new byte[2])};
        Assert.Throws<InvalidDataException>(()=>WgcDesktopComposition.Compose([Left],[invalid]));
    }
    [Fact] public async Task OlderMonitorIsReacquiredWithinSharedDeadline()
    {
        var calls=new Dictionary<string,int>();var now=TimeSpan.FromSeconds(11);
        Task<WgcMonitorFrame> Capture(MonitorInfo m,CancellationToken token)
        {
            calls[m.Id]=calls.GetValueOrDefault(m.Id)+1;
            return Task.FromResult(Frame(m,10,m==Left&&calls[m.Id]==1?TimeSpan.FromSeconds(10.1):TimeSpan.FromSeconds(10.9)));
        }
        var clockCalls=0;
        var capture=new WindowsGraphicsDesktopCapture(()=>[Left,Right],Capture,_=>{},()=>clockCalls++==0?TimeSpan.FromSeconds(10):now);
        var result=await capture.CaptureAsync();Assert.Equal(2,calls[Left.Id]);Assert.Equal(1,calls[Right.Id]);Assert.Equal(2,result.Monitors.Count);
    }
    [Fact] public async Task TopologyChangesNeverPublishPartialDesktop()
    {
        var reads=0;var capture=new WindowsGraphicsDesktopCapture(()=>++reads==1?[Left]:[Right],
            (m,_)=>Task.FromResult(Frame(m,10)),_=>{},()=>TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<InvalidDataException>(()=>capture.CaptureAsync());
    }
    [Fact] public async Task StaleFramesStopAtSharedDeadline()
    {
        var ticks=0;var capture=new WindowsGraphicsDesktopCapture(()=>[Left],(m,_)=>Task.FromResult(Frame(m,10,TimeSpan.Zero)),_=>{},()=>TimeSpan.FromSeconds(10+ticks++));
        await Assert.ThrowsAsync<CaptureSourceUnavailableException>(()=>capture.CaptureAsync());Assert.InRange(ticks,2,7);
    }
    [Fact] public async Task UnsupportedColorNeverStartsSession()
    {
        var capture=new WindowsGraphicsDesktopCapture(()=>[Left],(_,_)=>throw new Exception("Must not capture"),_=>throw new CaptureSourceUnavailableException("HDR"));
        await Assert.ThrowsAsync<CaptureSourceUnavailableException>(()=>capture.CaptureAsync());
    }
    [Fact] public async Task UserCancellationRemainsCancellation()
    {
        using var cts=new CancellationTokenSource();
        var capture=new WindowsGraphicsDesktopCapture(()=>[Left],(m,t)=>{cts.Cancel();t.ThrowIfCancellationRequested();return Task.FromResult(Frame(m,10));},_=>{});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>capture.CaptureAsync(cts.Token));
    }
    [Fact] public async Task FutureTimestampIsInvalidData()
    {
        var capture=new WindowsGraphicsDesktopCapture(()=>[Left],(m,_)=>Task.FromResult(Frame(m,10,TimeSpan.FromSeconds(11))),_=>{},()=>TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<InvalidDataException>(()=>capture.CaptureAsync());
    }
    [Fact] public async Task RouterSuppliesFreshNativePixelsToQuickCopyAndEditor()
    {
        byte value=10;var source=new WindowsGraphicsDesktopCapture(()=>[Left],
            (m,_)=>Task.FromResult(Frame(m,value)),_=>{},()=>TimeSpan.FromSeconds(10));
        var router=new ScreenshotCaptureRouter(source,source,()=>CaptureBackend.WindowsGraphicsCapture);
        var session=new CaptureModeSession(DesktopLayout.FromMonitors([Left]),router.CaptureAsync);
        Assert.Equal(10,(await session.ScreenshotAsync(default)).Frame.Bgra32[0]);value=20;
        await session.EnterEditorAsync(default);value=30;
        Assert.Equal(20,(await session.ScreenshotAsync(default)).Frame.Bgra32[0]);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceFailureCannotHideOtherMonitorDataOrAllocationFailure(bool allocation)
    {
        Exception fatal=allocation?new OutOfMemoryException("Synthetic allocation failure"):new InvalidDataException("Synthetic topology failure");
        var source=new WindowsGraphicsDesktopCapture(()=>[Left,Right],
            (m,_)=>Task.FromException<WgcMonitorFrame>(m==Left?new CaptureSourceUnavailableException("Synthetic unavailable"):fatal),_=>{});
        var router=new ScreenshotCaptureRouter(source,new ForbiddenFallback(),()=>CaptureBackend.Automatic);
        var error=await Record.ExceptionAsync(()=>router.CaptureAsync());Assert.Same(fatal,error);
    }
    private sealed class ForbiddenFallback:ShareVibeX.Core.Platform.IDesktopCapture
    {
        public IReadOnlyList<MonitorInfo> GetMonitors()=>[Left,Right];
        public Task<DesktopSnapshot> CaptureAsync(CancellationToken token=default)=>throw new Exception("Fallback must not run");
    }
}
