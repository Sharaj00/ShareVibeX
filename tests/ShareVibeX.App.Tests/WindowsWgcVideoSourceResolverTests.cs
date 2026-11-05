using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using ShareVibeX.Platform.Windows.Capture;
using ShareVibeX.Platform.Windows.Recording;

namespace ShareVibeX.App.Tests;

public sealed class WindowsWgcVideoSourceResolverTests
{
    private static readonly MonitorInfo Monitor=new("a",new(-100,0,100,100),1.25);
    [Fact] public void ResolverChecksSelectedSdrTargetsAndRechecksTopology()
    {
        var reads=0;IReadOnlyList<MonitorInfo>? colors=null;
        var resolver=new WindowsWgcVideoSourceResolver(()=>{reads++;return [Monitor];},m=>new(m,123),m=>colors=m);
        var result=resolver.Resolve(new(-90,0,20,30),new());
        Assert.Equal(2,reads);Assert.Equal(new[]{Monitor},colors);Assert.Contains("hmonitor=123",result.InputGraph);
    }
    [Fact] public void ChangedLayoutAndWrongHandleTargetCannotPublishSource()
    {
        var reads=0;
        var resolver=new WindowsWgcVideoSourceResolver(()=>++reads==1?[Monitor]:[Monitor with{Scale=2}],m=>new(m,123),_=>{});
        Assert.Throws<InvalidDataException>(()=>resolver.Resolve(Monitor.Bounds,new()));
        resolver=new WindowsWgcVideoSourceResolver(()=>[Monitor],m=>new(m with{Scale=2},123),_=>{});
        Assert.Throws<InvalidDataException>(()=>resolver.Resolve(Monitor.Bounds,new()));
    }
    [Fact] public void UnsupportedColorAndCustomCommandsNeverResolveHandles()
    {
        var resolver=new WindowsWgcVideoSourceResolver(()=>[Monitor],_=>throw new Exception("Must not resolve"),_=>throw new CaptureSourceUnavailableException("HDR"));
        Assert.Throws<CaptureSourceUnavailableException>(()=>resolver.Resolve(Monitor.Bounds,new()));
        Assert.Throws<ArgumentException>(()=>resolver.Resolve(Monitor.Bounds,new(){UseCustomCommand=true}));
    }
}
