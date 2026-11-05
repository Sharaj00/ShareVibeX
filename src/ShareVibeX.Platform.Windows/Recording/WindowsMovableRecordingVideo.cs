using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Platform.Windows.Recording;

public sealed class WindowsMovableRecordingVideo : IMovableRecordingVideo
{
    private readonly PixelRect initial, bounds;
    private readonly CancellationTokenSource lifetime;
    private readonly NamedPipeServerStream pipe;
    private long origin;
    public IReadOnlyList<string> Arguments { get; }
    public Task Completion { get; }

    public WindowsMovableRecordingVideo(PixelRect region, MonitorInfo monitor, RecordingOptions options, CancellationToken token)
    {
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
        nint screen=0,memory=0,bitmap=0,previous=0;
        try
        {
            pipe.WaitForConnectionAsync(token).GetAwaiter().GetResult();
            var bytes=new byte[checked(initial.Width*initial.Height*4)];
            screen=NativeMethods.GetDC(0);
            if(screen==0)throw new Win32Exception();
            memory=NativeMethods.CreateCompatibleDC(screen);
            if(memory==0)throw new Win32Exception();
            var info=new NativeMethods.BitmapInfo{Size=(uint)Marshal.SizeOf<NativeMethods.BitmapInfo>(),
                Width=initial.Width,Height=-initial.Height,Planes=1,BitCount=32,SizeImage=(uint)bytes.Length};
            bitmap=NativeMethods.CreateDIBSection(screen,ref info,0,out var bits,0,0);
            if(bitmap==0||bits==0)throw new Win32Exception();
            previous=NativeMethods.SelectObject(memory,bitmap);
            if(previous==0||previous==-1)throw new Win32Exception();
            var clock=Stopwatch.StartNew();
            for(long frame=0;;frame++)
            {
                token.ThrowIfCancellationRequested();
                var due=TimeSpan.FromSeconds(frame/(double)options.FrameRate);
                var wait=due-clock.Elapsed;
                if(wait>TimeSpan.Zero&&token.WaitHandle.WaitOne(wait))token.ThrowIfCancellationRequested();
                if(clock.Elapsed-due>TimeSpan.FromSeconds(2))throw new IOException(Loc.T("Live video fell behind its frame clock."));
                var current=Interlocked.Read(ref origin);
                var x=(int)(current>>32);var y=(int)current;
                if(!NativeMethods.BitBlt(memory,0,0,initial.Width,initial.Height,screen,x,y,0x40CC0020))
                    throw new Win32Exception(Marshal.GetLastWin32Error(),Loc.T("Desktop capture failed."));
                if(options.IncludeCursor)DrawCursor(memory,x,y);
                GdiFlush();
                Marshal.Copy(bits,bytes,0,bytes.Length);
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
        }
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
