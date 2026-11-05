using System.Diagnostics;
using ShareVibeX.Core.Capture;

namespace ShareVibeX.Platform.Windows.Capture;

/// <summary>Coordinates a single request, including callback drain before native disposal.</summary>
public sealed class WgcMonitorCapture
{
    private readonly Func<WgcCaptureTarget,IWgcMonitorSession> create;
    private readonly Func<MonitorInfo,WgcCaptureTarget> resolve;
    private readonly TimeSpan timeout;
    public WgcMonitorCapture(Func<WgcCaptureTarget,IWgcMonitorSession>? create=null,
        Func<MonitorInfo,WgcCaptureTarget>? resolve=null,TimeSpan? timeout=null)
    {
        this.create=create??WgcInterop.OpenSession;this.resolve=resolve??WindowsMonitorProvider.GetCaptureTarget;
        this.timeout=timeout??TimeSpan.FromSeconds(5);
        if(this.timeout<=TimeSpan.Zero||this.timeout>TimeSpan.FromSeconds(5))throw new ArgumentOutOfRangeException(nameof(timeout));
    }
    public Task<WgcMonitorFrame> CaptureAsync(MonitorInfo monitor,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(monitor);
        if(monitor.Bounds.IsEmpty||!double.IsFinite(monitor.Scale)||monitor.Scale<=0)throw new ArgumentException("Invalid capture monitor.",nameof(monitor));
        return Task.Run(()=>CaptureCoreAsync(monitor,token),token);
    }
    private async Task<WgcMonitorFrame> CaptureCoreAsync(MonitorInfo monitor,CancellationToken token)
    {
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);lifetime.CancelAfter(timeout);
        var completion=new TaskCompletionSource<WgcMonitorFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate=new object();var closing=false;var reading=false;IWgcMonitorSession? session=null;Exception? failure=null;
        void FrameArrived()
        {
            lock(gate){if(closing||reading||completion.Task.IsCompleted)return;reading=true;}
            try
            {
                lifetime.Token.ThrowIfCancellationRequested();var frame=session!.TryReadFrame(lifetime.Token);
                if(frame is not null)
                {
                    Validate(frame,monitor);lifetime.Token.ThrowIfCancellationRequested();completion.TrySetResult(frame);
                }
            }
            catch(OperationCanceledException)when(lifetime.IsCancellationRequested){completion.TrySetCanceled(lifetime.Token);}
            catch(Exception error){completion.TrySetException(error);}
            finally{lock(gate){reading=false;if(closing)drained.TrySetResult();}}
        }
        try
        {
            var target=resolve(monitor);
            if(target.Handle==0||target.Monitor!=monitor)throw new InvalidDataException(Loc.T("The display layout changed. Start a new capture."));
            lifetime.Token.ThrowIfCancellationRequested();session=create(target);session.FrameArrived+=FrameArrived;
            session.Start(captureCursor:false);
            var result=await completion.Task.WaitAsync(lifetime.Token).ConfigureAwait(false);token.ThrowIfCancellationRequested();return result;
        }
        catch(OperationCanceledException error)when(!token.IsCancellationRequested&&lifetime.IsCancellationRequested)
        {failure=new CaptureSourceUnavailableException(Loc.T("Timed out waiting for a WGC frame."),error);throw failure;}
        catch(Exception error){failure=error;throw;}
        finally
        {
            lock(gate){closing=true;if(!reading)drained.TrySetResult();}
            lifetime.Cancel();
            if(session is not null)
            {
                session.FrameArrived-=FrameArrived;
                await drained.Task.ConfigureAwait(false);
                if(completion.Task.IsFaulted)_=completion.Task.Exception;
                try{session.Dispose();}catch(Exception error)when(failure is not null){Trace.TraceError("WGC cleanup: "+error);}
            }
        }
    }
    private static void Validate(WgcMonitorFrame result,MonitorInfo monitor)
    {
        var frame=result.Frame;var stride=checked(monitor.Bounds.Width*4);var length=checked(stride*monitor.Bounds.Height);
        if(result.Monitor!=monitor||result.Timestamp<TimeSpan.Zero||frame.Width!=monitor.Bounds.Width||frame.Height!=monitor.Bounds.Height||frame.Stride!=stride||frame.Bgra32.Length!=length)
            throw new InvalidDataException(Loc.T("The WGC frame does not match the capture monitor."));
    }
}
