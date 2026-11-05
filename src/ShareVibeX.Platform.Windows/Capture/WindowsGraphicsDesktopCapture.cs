using System.Diagnostics;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.Platform.Windows.Capture;

public sealed class WindowsGraphicsDesktopCapture : IDesktopCapture
{
    private readonly Func<IReadOnlyList<MonitorInfo>> monitors;
    private readonly Func<MonitorInfo,CancellationToken,Task<WgcMonitorFrame>> capture;
    private readonly Action<IReadOnlyList<MonitorInfo>> ensureSdr;
    private readonly Func<TimeSpan> clock;
    public WindowsGraphicsDesktopCapture(Func<IReadOnlyList<MonitorInfo>>? monitors=null,
        Func<MonitorInfo,CancellationToken,Task<WgcMonitorFrame>>? capture=null,
        Action<IReadOnlyList<MonitorInfo>>? ensureSdr=null,Func<TimeSpan>? clock=null)
    {
        this.monitors=monitors??WindowsMonitorProvider.GetMonitors;
        this.capture=capture??new WgcMonitorCapture().CaptureAsync;
        this.ensureSdr=ensureSdr??WindowsCaptureColorCapabilities.EnsureSdr;
        this.clock=clock??(()=>TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp()/Stopwatch.Frequency));
    }
    public IReadOnlyList<MonitorInfo> GetMonitors()=>monitors();
    public async Task<DesktopSnapshot> CaptureAsync(CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var start=clock();
        try
        {
            var layout=GetMonitors().OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray();
            if(layout.Length==0)throw new InvalidOperationException(Loc.T("No displays are available."));
            ensureSdr(layout);deadline.Token.ThrowIfCancellationRequested();
            async Task<WgcMonitorFrame> Acquire(MonitorInfo monitor)
            {
                return await capture(monitor,deadline.Token).ConfigureAwait(false);
            }
            var frames=await AcquireAll(layout).ConfigureAwait(false);
            async Task<WgcMonitorFrame[]> AcquireAll(IEnumerable<MonitorInfo> requested)
            {
                var all=Task.WhenAll(requested.Select(Acquire));
                try{return await all.ConfigureAwait(false);}
                catch
                {
                    // A source failure must not hide invalid data or allocation failure on another monitor.
                    var fatal=all.Exception?.Flatten().InnerExceptions.FirstOrDefault(e=>e is not CaptureSourceUnavailableException);
                    if(fatal is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(fatal).Throw();
                    throw;
                }
            }
            while(true)
            {
                deadline.Token.ThrowIfCancellationRequested();var now=clock();
                if(now-start>=TimeSpan.FromSeconds(5))throw new CaptureSourceUnavailableException(Loc.T("Timed out waiting for synchronized WGC frames."));
                for(var i=0;i<frames.Length;i++)
                    if(frames[i].Monitor!=layout[i]||frames[i].Timestamp<TimeSpan.Zero||frames[i].Timestamp>now)
                        throw new InvalidDataException(Loc.T("The WGC frame does not match the capture monitor."));
                var newest=frames.Max(f=>f.Timestamp);
                var stale=Enumerable.Range(0,frames.Length).Where(i=>frames[i].Timestamp<start||newest-frames[i].Timestamp>TimeSpan.FromMilliseconds(100)).ToArray();
                if(stale.Length==0)break;
                var fresh=await AcquireAll(stale.Select(i=>layout[i])).ConfigureAwait(false);
                for(var i=0;i<stale.Length;i++)frames[stale[i]]=fresh[i];
            }
            if(!layout.SequenceEqual(GetMonitors().OrderBy(m=>m.Id,StringComparer.Ordinal)))
                throw new InvalidDataException(Loc.T("The display layout changed. Start a new capture."));
            ensureSdr(layout);deadline.Token.ThrowIfCancellationRequested();
            return WgcDesktopComposition.Compose(layout,frames,deadline.Token);
        }
        catch(OperationCanceledException error)when(!cancellationToken.IsCancellationRequested&&deadline.IsCancellationRequested)
        {throw new CaptureSourceUnavailableException(Loc.T("Timed out waiting for synchronized WGC frames."),error);}
    }
}
