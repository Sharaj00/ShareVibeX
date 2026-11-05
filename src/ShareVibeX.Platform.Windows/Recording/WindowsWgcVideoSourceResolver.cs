using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using ShareVibeX.Platform.Windows.Capture;

namespace ShareVibeX.Platform.Windows.Recording;

public sealed class WindowsWgcVideoSourceResolver
{
    private readonly Func<IReadOnlyList<MonitorInfo>> monitors;
    private readonly Func<MonitorInfo,WgcCaptureTarget> target;
    private readonly Action<IReadOnlyList<MonitorInfo>> ensureSdr;
    public WindowsWgcVideoSourceResolver(Func<IReadOnlyList<MonitorInfo>>? monitors=null,
        Func<MonitorInfo,WgcCaptureTarget>? target=null,Action<IReadOnlyList<MonitorInfo>>? ensureSdr=null)
    {
        this.monitors=monitors??WindowsMonitorProvider.GetMonitors;
        this.target=target??WindowsMonitorProvider.GetCaptureTarget;
        this.ensureSdr=ensureSdr??WindowsCaptureColorCapabilities.EnsureSdr;
    }
    public WgcVideoSource Resolve(PixelRect region,RecordingOptions options)
    {
        RecordingValidation.Validate(options);
        if(options.UseCustomCommand)throw new ArgumentException(Loc.T("Custom recording commands cannot use a managed WGC source."));
        var layout=monitors().OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray();
        var selected=layout.Where(m=>!m.Bounds.Intersect(region).IsEmpty).ToArray();
        ensureSdr(selected);
        var targets=selected.Select(m=>
        {
            var resolved=target(m);
            if(resolved.Monitor!=m||resolved.Handle==0)throw new InvalidDataException(Loc.T("The display layout changed. Start a new capture."));
            return new WgcVideoMonitor(m,unchecked((ulong)(nuint)resolved.Handle));
        }).ToArray();
        if(!layout.SequenceEqual(monitors().OrderBy(m=>m.Id,StringComparer.Ordinal)))throw new InvalidDataException(Loc.T("The display layout changed. Start a new capture."));
        return WgcVideoSource.Create(region,targets,options.FrameRate,options.IncludeCursor);
    }
}
