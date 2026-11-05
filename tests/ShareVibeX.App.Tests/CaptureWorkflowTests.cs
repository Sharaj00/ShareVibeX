using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Platform;

namespace ShareVibeX.App.Tests;

public sealed class CaptureWorkflowTests
{
    private static DesktopSnapshot Snapshot() => new(new(-100,0,100,100), [new("screen",new(-100,0,100,100),1)],new(100,100,400,new byte[40000]));

    [Fact]
    public async Task ConcurrentRequestsProduceOnlyOneResult()
    {
        var snapshot = Snapshot(); var source = new Source(snapshot); var ui = new Presentation();
        var coordinator = new CaptureCoordinator(); var workflow = new CaptureWorkflow(coordinator,source,ui);
        var first = workflow.StartAsync();
        await workflow.StartAsync();
        Assert.Equal(CaptureState.Ready,coordinator.State);
        Assert.Single(ui.Results);
        Assert.Same(ui.Captured, ui.Results[0].Snapshot);
        Assert.Equal(new PixelRect(-90,10,20,20),ui.Results[0].Region);
        ui.Done.SetResult(); await first;
        Assert.Equal(CaptureState.Idle,coordinator.State);
        Assert.Equal(0,source.Captures);
        Assert.Equal(new[] {"hide", "select", "result", "restore"},ui.Events);
    }

    [Fact]
    public async Task DelayedSelectionAfterCancellationCannotShowResult()
    {
        var source = new Source(Snapshot());
        var ui = new Presentation { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) }; var coordinator = new CaptureCoordinator();
        var workflow = new CaptureWorkflow(coordinator,source,ui);
        var running = workflow.StartAsync(); workflow.Cancel(); ui.Pending.SetResult(new(Snapshot(), new(-90,10,20,20)));
        await running;
        Assert.Empty(ui.Results); Assert.Contains("select",ui.Events); Assert.Equal(0,source.Captures);
        Assert.Equal(CaptureState.Idle,coordinator.State); Assert.Contains("restore",ui.Events);
    }

    [Fact]
    public async Task EscapeSelectionRestoresAndReleasesSession()
    {
        var ui = new Presentation { Region = null }; var coordinator = new CaptureCoordinator();
        await new CaptureWorkflow(coordinator,new Source(Snapshot()),ui).StartAsync();
        Assert.Equal(CaptureState.Idle,coordinator.State); Assert.Null(coordinator.Session);
        Assert.Equal(new[] {"hide","select","restore"},ui.Events);
    }

    [Fact]
    public async Task ChangedDisplayTopologyCancelsBeforeResult()
    {
        var source = new Source(Snapshot()); var ui = new Presentation { BeforeSelection = () => source.Changed = true };
        var coordinator = new CaptureCoordinator();
        await new CaptureWorkflow(coordinator,source,ui).StartAsync();
        Assert.Empty(ui.Results); Assert.Contains("display",ui.Error!,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CaptureState.Idle,coordinator.State);
    }

    private sealed class Source(DesktopSnapshot snapshot) : IDesktopCapture
    {
        public int Captures; public bool Changed;
        public IReadOnlyList<MonitorInfo> GetMonitors() => Changed ? [new("other",new(0,0,10,10),1)] : snapshot.Monitors;
        public Task<DesktopSnapshot> CaptureAsync(CancellationToken token = default) { Captures++; return Task.FromResult(snapshot); }
    }

    [Fact]
    public async Task CleanupFailureIsReportedWithoutTerminatingCaptureEntry()
    {
        var ui=new Presentation{Region=null,RestoreError=new InvalidOperationException("closed window")};
        var coordinator=new CaptureCoordinator();var workflow=new CaptureWorkflow(coordinator,new Source(Snapshot()),ui);
        Assert.Null(await Record.ExceptionAsync(()=>workflow.StartAsync()));
        Assert.Equal(CaptureState.Idle,coordinator.State);Assert.Contains("closed window",ui.Error);
        ui.RestoreError=null;Assert.Null(await Record.ExceptionAsync(()=>workflow.StartAsync()));
    }

    private sealed class Presentation : ICapturePresentation
    {
        public List<string> Events = [];
        public List<CaptureSession> Results = [];
        public DesktopSnapshot Captured = Snapshot();
        public PixelRect? Region = new(-90,10,20,20);
        public Action? BeforeSelection;
        public string? Error;
        public Exception? RestoreError;
        public TaskCompletionSource Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<CaptureSession?>? Pending;
        public Task HideAndWaitAsync(CancellationToken token) { Events.Add("hide"); return Task.CompletedTask; }
        public Task<CaptureSession?> SelectAsync(DesktopLayout layout, CancellationToken token) { Events.Add("select"); BeforeSelection?.Invoke(); return Pending?.Task ?? Task.FromResult(Region is {} region ? new CaptureSession(Captured,region) : null); }
        public Task ShowResultAsync(CaptureSession session, CancellationToken token) { Events.Add("result"); Results.Add(session); return Done.Task.WaitAsync(token); }
        public void Restore() {Events.Add("restore");if(RestoreError is not null)throw RestoreError;}
        public void ShowError(string message) => Error = message;
    }
}
