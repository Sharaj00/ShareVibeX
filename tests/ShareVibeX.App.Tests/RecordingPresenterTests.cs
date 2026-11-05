using Avalonia.Controls;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;
using Avalonia.LogicalTree;
using ShareVibeX.App.Playback;
using ShareVibeX.Core.Playback;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class RecordingPresenterTests(UiTestFixture ui)
{
    [Fact]
    public Task MissingMicrophoneRejectsStartupBeforeRecorderOrCaptureFrame()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-no-microphone-"+Guid.NewGuid().ToString("N"));
        var factory=new Factory();var service=new RecordingService(new(new Probe(),factory,new Audio(),_=>"fixture"),root);
        using var presenter=new RecordingControlPresenter(service,()=>[new("screen",new(0,0,800,600),1)],exclude:_=>true){MicrophoneProbe=(_,_)=>Task.FromResult(false)};
        try
        {
            var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>presenter.StartAsync(new(new(),new(20,30,100,100),new(false,true)),default));
            Assert.Equal(MicrophoneAvailability.UnavailableMessage,error.Message);Assert.Null(factory.Path);Assert.False(service.IsActive);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task EarlyStopOrCancelDoesNotStartRecording(bool cancel)=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-early-stop",Guid.NewGuid().ToString("N"));var factory=new Factory();
        var service=new RecordingService(new(new Probe(),factory,new Audio(),_=>"fixture"),root);
        using var presenter=new RecordingControlPresenter(service,()=>[new("screen",new(0,0,800,600),1)],(stop,abort,close)=>
        {var window=new RecordingControlWindow(stop,abort,close);window.Opened+=(_,args)=>{_=(cancel?abort():stop());};return window;},_=>true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>presenter.StartAsync(new(new(),new(10,20,100,100),new(false,false)),default).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.False(service.IsActive);Assert.Null(factory.Path);if(Directory.Exists(root))Directory.Delete(root,true);
    });
    [Fact]
    public Task RecoveryUsesCurrentTrustedToolInsteadOfTheManifestExecutable()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-recovery-tool",Guid.NewGuid().ToString("N"));
        var workspace=RecordingWorkspace.Create(root,new(){FfmpegPath="manifest-untrusted.exe"},new(0,0,100,100));await File.WriteAllTextAsync(workspace.CapturePath,"video");
        await workspace.WriteManifestAsync("Result",new(true,workspace.CapturePath,null,null),segments:[],hasAudio:false);
        var entry=Assert.Single(new ShareVibeX.App.Recovery.RecordingRecoveryStore(root,Path.Combine(root,"cache")).List());
        var preview=new Preview();var service=new RecordingService(new(new Probe(),new Factory(),new Audio(),_=>"fixture"),root);
        using var presenter=new RecordingControlPresenter(service,()=>[new("screen",new(0,0,800,600),1)],preview:preview);
        try
        {
            await presenter.RecoverAsync(entry,"current-approved.exe");Assert.Equal("current-approved.exe",preview.Media!.FfmpegPath);Assert.Equal("current-approved.exe",service.Session.Options.FfmpegPath);
            preview.Release.TrySetResult();await presenter.ClosePreviewAsync();
        }
        finally{preview.Release.TrySetResult();if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Fact]
    public Task StoppedRecordingHandsOffToPreviewAndCloseWaitsBeforeReleasingOwnership()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-preview-handoff",Guid.NewGuid().ToString("N"));
        var factory=new Factory();var service=new RecordingService(new(new Probe(),factory,new Audio(),_=>"fixture"),root);var preview=new Preview();
        RecordingControlWindow? control=null;
        using var presenter=new RecordingControlPresenter(service,()=>[new("screen",new(0,0,800,600),1)],(stop,cancel,close)=>control=new(stop,cancel,close),_=>true,preview);
        try
        {
            await presenter.StartAsync(new(new(),new(20,30,100,100),new(false,false)),default);
            await File.WriteAllTextAsync(factory.Path!,"video");await service.StopAsync();
            Assert.True(preview.Shown);Assert.False(control!.IsVisible);Assert.True(service.HasPendingResult);
            Assert.Equal(service.Session.Result!.OutputPath,preview.Media!.Path);
            var closing=presenter.ClosePreviewAsync();Assert.False(closing.IsCompleted);Assert.True(service.HasPendingResult);
            preview.Release.TrySetResult();await closing;Assert.False(service.HasPendingResult);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Fact]
    public Task ExitCancelsPendingPreviewInitializationBeforeWaiting()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-preview-exit",Guid.NewGuid().ToString("N"));
        var workspace=RecordingWorkspace.Create(root,new(),new(0,0,100,100));await File.WriteAllTextAsync(workspace.CapturePath,"video");
        await workspace.WriteManifestAsync("Result",new(true,workspace.CapturePath,null,null),segments:[],hasAudio:false);
        var entry=Assert.Single(new ShareVibeX.App.Recovery.RecordingRecoveryStore(root,Path.Combine(root,"cache")).List());
        var preview=new OpeningPreview();var service=new RecordingService(new(new Probe(),new Factory(),new Audio(),_=>"fixture"),root);
        using var presenter=new RecordingControlPresenter(service,()=>[new("screen",new(0,0,800,600),1)],preview:preview);
        try
        {
            var opening=presenter.RecoverAsync(entry,"fixture");await preview.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(await presenter.RequestExitAsync().WaitAsync(TimeSpan.FromSeconds(2)));
            await opening.WaitAsync(TimeSpan.FromSeconds(2));Assert.True(preview.Canceled);Assert.False(service.HasPendingResult);
            Assert.True(File.Exists(workspace.CapturePath),"Canceling preview setup on exit must retain recovery media.");
        }
        finally{preview.Release.TrySetResult();await presenter.ClosePreviewAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    private sealed class OpeningPreview:IRecordingPreviewHost
    {
        public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Canceled;public event Action? Closed;
        public async Task ShowAsync(PreviewMedia media,ShareVibeX.Core.Capture.PixelRect region,IReadOnlyList<MonitorInfo> monitors,CancellationToken token)
        {
            Started.TrySetResult();try{await Release.Task.WaitAsync(token);}catch(OperationCanceledException){Canceled=true;throw;}
        }
        public Task CloseAsync(){Release.TrySetResult();Closed?.Invoke();return Task.CompletedTask;}
        public void Activate(){}
    }
    private sealed class Preview:IRecordingPreviewHost
    {
        public bool Shown;public PreviewMedia? Media;public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action? Closed;
        public Task ShowAsync(PreviewMedia media,ShareVibeX.Core.Capture.PixelRect region,IReadOnlyList<MonitorInfo> monitors,CancellationToken token){Shown=true;Media=media;return Task.CompletedTask;}
        public async Task CloseAsync(){await Release.Task;Closed?.Invoke();}public void Activate(){}
    }
    [Fact]
    public Task PresenterConnectsProgressStopAndRetainedResultWithoutDestinationPicker()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-presenter",Guid.NewGuid().ToString("N"));
        var factory=new Factory();var service=new RecordingService(new(new Probe(),factory,new Audio(),_=>"fixture"),root);
        RecordingControlWindow? control=null;
        using var presenter=new RecordingControlPresenter(service,()=>[new("screen",new(0,0,800,600),1)],
            (stop,cancel,close)=>control=new(stop,cancel,close),_=>true);
        try
        {
            await presenter.StartAsync(new(new(),new(20,30,100,100),new(false,false)),default);
            Assert.True(service.IsActive);Assert.True(control!.IsVisible);
            await File.WriteAllTextAsync(factory.Path!,"fixture video");await service.StopAsync();
            Assert.True(service.HasPendingResult);Assert.True(control.IsVisible);
            service.Session.ReleaseResult();Assert.False(control.IsVisible);Assert.False(service.HasPendingResult);
        }
        finally{control?.Dismiss();if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Fact]
    public Task ExclusionRefusalPreventsRecorderStartupAndTopologyChangeStopsAnActiveRun()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-presenter",Guid.NewGuid().ToString("N"));
        var factory=new Factory();var service=new RecordingService(new(new Probe(),factory,new Audio(),_=>"fixture"),root);
        IReadOnlyList<MonitorInfo> monitors=[new("screen",new(0,0,800,600),1)];
        try
        {
            using(var refused=new RecordingControlPresenter(service,()=>monitors,exclude:_=>false))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(()=>refused.StartAsync(new(new(),monitors[0].Bounds,new(false,false)),default));
                Assert.Null(factory.Path);Assert.False(service.IsActive);
            }
            RecordingControlWindow? control=null;
            using var presenter=new RecordingControlPresenter(service,()=>monitors,(stop,cancel,close)=>control=new(stop,cancel,close),exclude:_=>true);
            await presenter.StartAsync(new(new(),new(20,30,100,100),new(false,false)),default);
            await File.WriteAllTextAsync(factory.Path!,"fixture video");monitors=[new("changed",new(0,0,1024,768),1)];
            await presenter.RefreshAsync();Assert.False(service.IsActive);Assert.Equal(1,factory.Process.Stops);
            Assert.Contains("display layout changed",string.Join(" ",control!.GetLogicalDescendants().OfType<TextBlock>().Select(b=>b.Text)),StringComparison.OrdinalIgnoreCase);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    private sealed class Probe:ICommandRunner
    {
        public Task<ToolResult> RunAsync(string exe,IReadOnlyList<string> args,CancellationToken token)=>Task.FromResult(new ToolResult(0,args.Last() switch{
            "-version"=>"ffmpeg version fixture","-devices"=>" D  gdigrab input","-encoders"=>" V....D libx264 encoder","encoder=libx264"=>"Encoder libx264 [H264]:\n -preset <string> E..V....... preset\n -tune <string> E..V....... tune\n -crf <float> E..V....... quality (from -1 to 51)\n",
            "-filters"=>" .. pad V->V\n .. format V->V",_=>""},""));
    }
    private sealed class Factory:IRecordingProcessFactory
    {
        public string? Path;public Process Process=new();
        public IRecordingProcess Start(string exe,IReadOnlyList<string> args){Path=args.Last();return Process;}
    }
    private sealed class Process:IRecordingProcess
    {
        public Task Ready=>Task.CompletedTask;private readonly TaskCompletionSource<int> ended=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<int> Exit=>ended.Task;public string Diagnostics=>"";public int Stops;
        public Task RequestStopAsync(){Stops++;ended.TrySetResult(0);return Task.CompletedTask;}public void Kill()=>ended.TrySetResult(-1);public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Audio:IRecordingAudioFactory
    {
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token)=>throw new InvalidOperationException("No audio permitted.");
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>throw new InvalidOperationException("No devices permitted.");
    }
}
