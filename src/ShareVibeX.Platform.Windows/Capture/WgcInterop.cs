using System.Diagnostics;
using System.Runtime.InteropServices;
using ShareVibeX.Core.Capture;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace ShareVibeX.Platform.Windows.Capture;

public static class WgcInterop
{
    public static IWgcMonitorSession OpenSession(WgcCaptureTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        try { return new NativeSession(target); }
        catch (Exception ex) when (IsOutOfMemory(ex)) { throw new OutOfMemoryException(Loc.T("WGC allocation failed."), ex); }
        catch (Exception ex) when (IsSourceFailure(ex))
        { throw new CaptureSourceUnavailableException(Loc.T("Windows Graphics Capture could not open the requested monitor."), ex); }
    }

    private static bool IsSourceFailure(Exception ex) =>
        ex is COMException or PlatformNotSupportedException or NotSupportedException or DllNotFoundException or EntryPointNotFoundException or SharpGen.Runtime.SharpGenException;

    private static bool IsOutOfMemory(Exception ex) => ex is not OutOfMemoryException && ex.HResult == unchecked((int)0x8007000E);

    private sealed class NativeSession : IWgcMonitorSession
    {
        private readonly object gate = new();
        private readonly WgcCaptureTarget target;
        private ID3D11Device? device;
        private ID3D11DeviceContext? context;
        private IDirect3DDevice? winrtDevice;
        private GraphicsCaptureItem? item;
        private Direct3D11CaptureFramePool? pool;
        private GraphicsCaptureSession? session;
        private bool disposed;
        private int targetClosed;
        public event Action? FrameArrived;

        public NativeSession(WgcCaptureTarget target)
        {
            this.target = target;
            try
            {
                if (!GraphicsCaptureSession.IsSupported()) throw new PlatformNotSupportedException(Loc.T("Windows Graphics Capture is not supported."));
                if (target.Handle == 0) throw new InvalidDataException(Loc.T("The requested monitor has no native handle."));
                item = CreateItem(target.Handle);
                ValidateSize(item.Size);
                ThrowForHr(D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, Array.Empty<FeatureLevel>(), out device, out context).Code);
                using var dxgi = device.QueryInterface<IDXGIDevice>();
                ThrowForHr(CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var inspectable));
                try { winrtDevice = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable); }
                finally { Marshal.Release(inspectable); }
                pool = Direct3D11CaptureFramePool.CreateFreeThreaded(winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
                session = pool.CreateCaptureSession(item);
                item.Closed += OnTargetClosed;
                pool.FrameArrived += OnFrameArrived;
            }
            catch
            {
                try { Dispose(); } catch (Exception cleanupError) { Trace.TraceError("WGC initialization cleanup: " + cleanupError); }
                throw;
            }
        }

        public void Start(bool captureCursor)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                ThrowIfTargetClosed();
                try
                {
                    session!.IsCursorCaptureEnabled = captureCursor;
                    session.StartCapture();
                }
                catch (Exception ex) when (IsOutOfMemory(ex)) { throw new OutOfMemoryException(Loc.T("WGC allocation failed."), ex); }
                catch (Exception ex) when (IsSourceFailure(ex))
                { throw new CaptureSourceUnavailableException(Loc.T("Windows Graphics Capture could not start."), ex); }
            }
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args) => FrameArrived?.Invoke();
        private void OnTargetClosed(GraphicsCaptureItem sender, object args)
        {
            Interlocked.Exchange(ref targetClosed, 1);
            FrameArrived?.Invoke();
        }

        private void ThrowIfTargetClosed()
        {
            if (Volatile.Read(ref targetClosed) != 0) throw new InvalidDataException(Loc.T("The requested capture monitor was closed."));
        }

        public WgcMonitorFrame? TryReadFrame(CancellationToken cancellationToken)
        {
            try { return ReadFrame(cancellationToken); }
            catch (Exception ex) when (IsOutOfMemory(ex)) { throw new OutOfMemoryException(Loc.T("WGC allocation failed."), ex); }
            catch (Exception ex) when (IsSourceFailure(ex))
            { throw new CaptureSourceUnavailableException(Loc.T("Windows Graphics Capture could not read the monitor frame."), ex); }
        }

        private unsafe WgcMonitorFrame? ReadFrame(CancellationToken cancellationToken)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfTargetClosed();
                using var frame = pool!.TryGetNextFrame();
                if (frame is null) return null;
                ValidateSize(frame.ContentSize);
                TimeSpan? relativeTime = frame.SystemRelativeTime;
                if (relativeTime is not TimeSpan timestamp || timestamp < TimeSpan.Zero)
                    throw new InvalidDataException(Loc.T("The capture frame has no valid timestamp."));
                using var source = GetTexture(frame.Surface);
                var desc = source.Description;
                int width = target.Monitor.Bounds.Width, height = target.Monitor.Bounds.Height;
                if (desc.Width < width || desc.Height < height || desc.Format != Format.B8G8R8A8_UNorm || desc.SampleDescription.Count != 1 || desc.MipLevels != 1 || desc.ArraySize != 1)
                    throw new InvalidDataException(Loc.T("The capture texture does not match the requested BGRA monitor frame."));
                // CopyResource requires matching texture dimensions, including any pool padding.
                var stagingDesc = new Texture2DDescription(Format.B8G8R8A8_UNorm, desc.Width, desc.Height, 1, 1, BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read);
                using var staging = device!.CreateTexture2D(stagingDesc);
                context!.CopyResource(staging, source);
                // Nonblocking Map polling does not submit the queued copy.
                context.Flush();
                var watch = Stopwatch.StartNew();
                MappedSubresource mapped;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ThrowIfTargetClosed();
                    var result = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.DoNotWait, out mapped);
                    if (result.Code != unchecked((int)0x887A000A)) { ThrowForHr(result.Code); break; }
                    if (watch.Elapsed >= TimeSpan.FromSeconds(5)) throw new CaptureSourceUnavailableException(Loc.T("Timed out reading the Windows Graphics Capture frame."));
                    Thread.Sleep(1);
                }
                try
                {
                    int pitch = checked((int)mapped.RowPitch);
                    if (mapped.DataPointer == 0 || pitch < checked(width * 4))
                        throw new InvalidDataException(Loc.T("The mapped capture texture has invalid row storage."));
                    int size = checked((height - 1) * pitch + checked(width * 4));
                    var pixels = WgcFrameReadback.CopyBgraRows(new ReadOnlySpan<byte>((void*)mapped.DataPointer, size), width, height, pitch, cancellationToken);
                    ThrowIfTargetClosed();
                    return new WgcMonitorFrame(target.Monitor, pixels, timestamp);
                }
                finally { context.Unmap(staging, 0); }
            }
        }

        private void ValidateSize(SizeInt32 size)
        {
            if (size.Width != target.Monitor.Bounds.Width || size.Height != target.Monitor.Bounds.Height)
                throw new InvalidDataException(Loc.T("The monitor geometry changed during Windows Graphics Capture."));
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                FrameArrived = null;
                Exception? failure = null;
                void Release(Action release)
                {
                    try { release(); } catch (Exception error) { failure ??= error; }
                }
                if (item is not null) Release(() => item.Closed -= OnTargetClosed);
                if (pool is not null) Release(() => pool.FrameArrived -= OnFrameArrived);
                Release(() => session?.Dispose());
                Release(() => pool?.Dispose());
                Release(() => winrtDevice?.Dispose());
                Release(() => context?.Dispose());
                Release(() => device?.Dispose());
                session = null; pool = null; winrtDevice = null; context = null; device = null; item = null;
                if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }

    private static unsafe GraphicsCaptureItem CreateItem(nint monitor)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        ThrowForHr(WindowsCreateString(className, className.Length, out var name));
        nint factory = 0, result = 0;
        try
        {
            var iid = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
            ThrowForHr(RoGetActivationFactory(name, in iid, out factory));
            var itemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            var method = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(nint**)factory)[4];
            ThrowForHr(method(factory, monitor, &itemIid, &result));
            return WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(result);
        }
        finally
        {
            if (result != 0) Marshal.Release(result);
            if (factory != 0) Marshal.Release(factory);
            WindowsDeleteString(name);
        }
    }

    private static unsafe ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        nint unknown = WinRT.MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        nint access = 0, texture = 0;
        try
        {
            var iid = new Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
            ThrowForHr(Marshal.QueryInterface(unknown, in iid, out access));
            var textureIid = typeof(ID3D11Texture2D).GUID;
            var method = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)(*(nint**)access)[3];
            ThrowForHr(method(access, &textureIid, &texture));
            return new ID3D11Texture2D(texture);
        }
        finally { if (access != 0) Marshal.Release(access); Marshal.Release(unknown); }
    }

    private static void ThrowForHr(int hr)
    {
        if (hr == unchecked((int)0x8007000E)) throw new OutOfMemoryException();
        Marshal.ThrowExceptionForHR(hr);
    }

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string sourceString, int length, out nint value);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(nint name, in Guid iid, out nint factory);
    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint device, out nint inspectable);
}
