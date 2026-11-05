using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using ShareVibeX.Platform.Windows.Capture;

namespace ShareVibeX.Platform.Windows.Recording;

public sealed class WindowsMovableRecordingVideo : IMovableRecordingVideo
{
    private readonly PixelRect initial, bounds;
    private readonly CancellationTokenSource lifetime;
    private readonly NamedPipeServerStream pipe;
    private readonly IWgcRegionSession? wgc;
    private long origin;
    public IReadOnlyList<string> Arguments { get; }
    public Task Completion { get; }

    public WindowsMovableRecordingVideo(PixelRect region,MonitorInfo monitor,RecordingOptions options,CancellationToken token)
        :this(region,monitor,options,token,null){}

    public static WindowsMovableRecordingVideo OpenWgc(PixelRect region,MonitorInfo monitor,RecordingOptions options,CancellationToken token)
    {
        WindowsCaptureColorCapabilities.EnsureSdr([monitor]);
        var session=WgcInterop.OpenRegionSession(WindowsMonitorProvider.GetCaptureTarget(monitor));
        try{return new(region,monitor,options,token,session);}
        catch{session.Dispose();throw;}
    }

    private WindowsMovableRecordingVideo(PixelRect region,MonitorInfo monitor,RecordingOptions options,CancellationToken token,IWgcRegionSession? wgc)
    {
        this.wgc=wgc;
        initial=region;
        bounds=monitor.Bounds;
        Move(region);
        var name="ShareVibeX-video-"+Guid.NewGuid().ToString("N");
        pipe=new(name,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,0,65536);
        lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);
        Arguments=["-thread_queue_size","8","-f","rawvideo","-pixel_format","bgr0","-video_size",$"{region.Width}x{region.Height}",
            "-framerate",options.FrameRate.ToString(CultureInfo.InvariantCulture),"-probesize","32","-analyzeduration","0","-i",@"\\.\pipe\"+name];
        Completion=Task.Factory.StartNew(()=>Pump(options,lifetime.Token),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
    }

    public void Move(PixelRect region)
    {
        if(region.IsEmpty||region.Width!=initial.Width||region.Height!=initial.Height||bounds.Intersect(region)!=region)
            throw new ArgumentOutOfRangeException(nameof(region));
        Interlocked.Exchange(ref origin,((long)region.X<<32)|(uint)region.Y);
    }

    private void Pump(RecordingOptions options,CancellationToken token)
    {
        nint screen=0,memory=0,bitmap=0,previous=0,bits=0;
        try
        {
            pipe.WaitForConnectionAsync(token).GetAwaiter().GetResult();
            var bytes=new byte[checked(initial.Width*initial.Height*4)];
            if(wgc is not null)
            {
                wgc.Start(options.IncludeCursor);
                var startup=Stopwatch.StartNew();
                while(!wgc.TryCopyRegion(CurrentRegion(),bytes,token))
                {
                    if(startup.Elapsed>=TimeSpan.FromSeconds(5))throw new CaptureSourceUnavailableException(Loc.T("Timed out reading the Windows Graphics Capture frame."));
                    if(token.WaitHandle.WaitOne(4))token.ThrowIfCancellationRequested();
                }
            }
            else
            {
                screen=NativeMethods.GetDC(0);
                if(screen==0)throw new Win32Exception();
                memory=NativeMethods.CreateCompatibleDC(screen);
                if(memory==0)throw new Win32Exception();
                var info=new NativeMethods.BitmapInfo{Size=(uint)Marshal.SizeOf<NativeMethods.BitmapInfo>(),
                    Width=initial.Width,Height=-initial.Height,Planes=1,BitCount=32,SizeImage=(uint)bytes.Length};
                bitmap=NativeMethods.CreateDIBSection(screen,ref info,0,out bits,0,0);
                if(bitmap==0||bits==0)throw new Win32Exception();
                previous=NativeMethods.SelectObject(memory,bitmap);
                if(previous==0||previous==-1)throw new Win32Exception();
            }
            var clock=Stopwatch.StartNew();
            for(long frame=0;;frame++)
            {
                token.ThrowIfCancellationRequested();
                var due=TimeSpan.FromSeconds(frame/(double)options.FrameRate);
                var wait=due-clock.Elapsed;
                if(wait>TimeSpan.Zero&&token.WaitHandle.WaitOne(wait))token.ThrowIfCancellationRequested();
                if(clock.Elapsed-due>TimeSpan.FromSeconds(2))throw new IOException(Loc.T("Live video fell behind its frame clock."));
                var region=CurrentRegion();
                if(wgc is not null)wgc.TryCopyRegion(region,bytes,token);
                else
                {
                    if(!NativeMethods.BitBlt(memory,0,0,initial.Width,initial.Height,screen,region.X,region.Y,0x40CC0020))
                        throw new Win32Exception();
                    if(options.IncludeCursor)DrawCursor(memory,region.X,region.Y);
                    GdiFlush();
                    Marshal.Copy(bits,bytes,0,bytes.Length);
                }
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                try{pipe.WriteAsync(bytes.AsMemory(),deadline.Token).AsTask().GetAwaiter().GetResult();}
                catch(OperationCanceledException)when(!token.IsCancellationRequested)
                {throw new TimeoutException(Loc.T("FFmpeg stopped consuming live video."));}
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(IOException error)when((error.HResult&0xffff) is 109 or 232 or 233){}
        finally
        {
            pipe.Dispose();
            if(previous!=0&&previous!=-1)NativeMethods.SelectObject(memory,previous);
            if(bitmap!=0)NativeMethods.DeleteObject(bitmap);
            if(memory!=0)NativeMethods.DeleteDC(memory);
            if(screen!=0)NativeMethods.ReleaseDC(0,screen);
            wgc?.Dispose();
        }
    }

    private PixelRect CurrentRegion()
    {
        var current=Interlocked.Read(ref origin);
        return initial with{X=(int)(current>>32),Y=(int)current};
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        try{await Completion.ConfigureAwait(false);}catch{ /* The run observes Completion. */ }
        finally{lifetime.Dispose();}
    }

    private static void DrawCursor(nint target,int x,int y)
    {
        var cursor=new CursorInfo{Size=Marshal.SizeOf<CursorInfo>()};
        if(!GetCursorInfo(ref cursor)||(cursor.Flags&1)==0||!GetIconInfo(cursor.Handle,out var icon))return;
        try{DrawIconEx(target,cursor.X-x-(int)icon.X,cursor.Y-y-(int)icon.Y,cursor.Handle,0,0,0,0,3);}
        finally
        {
            if(icon.Mask!=0)NativeMethods.DeleteObject(icon.Mask);
            if(icon.Color!=0)NativeMethods.DeleteObject(icon.Color);
        }
    }
    [StructLayout(LayoutKind.Sequential)]private struct CursorInfo { public int Size,Flags;public nint Handle;public int X,Y; }
    [StructLayout(LayoutKind.Sequential)]private struct IconInfo { public int IsIcon;public uint X,Y;public nint Mask,Color; }
    [DllImport("user32.dll")]private static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")]private static extern bool GetIconInfo(nint icon,out IconInfo info);
    [DllImport("user32.dll")]private static extern bool DrawIconEx(nint dc,int x,int y,nint icon,int w,int h,uint step,nint brush,uint flags);
    [DllImport("gdi32.dll")]private static extern bool GdiFlush();
}
