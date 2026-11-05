using ShareVibeX.Core.Recording;
using ShareVibeX.App.Recording;

namespace ShareVibeX.App.Tests;

public sealed class RecordingAssemblyTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"ShareVibeX-assembly",Guid.NewGuid().ToString("N"));
    [Fact]
    public void MultipleSegmentsNormalizeClocksAndExcludeAudioForSilentOutput()
    {
        var segments=new[]{new RecordingSegment("first.mp4",TimeSpan.FromSeconds(2),100,true,100,100),new RecordingSegment("second.mp4",TimeSpan.FromSeconds(3),200,true,100,100)};
        var command=RecordingAssemblyCommand.Build(new(){AdditionalArguments="-maxrate 5000k -bufsize 10000k"},segments,true,"final.partial.mp4");
        var graph=command[command.IndexOf("-filter_complex")+1];
        Assert.Contains("setpts=PTS-STARTPTS",graph);Assert.Contains("atrim=duration=2",graph);Assert.Contains("atrim=duration=3",graph);
        Assert.Contains("concat=n=2:v=1:a=1",graph);Assert.True(command.IndexOf("first.mp4")<command.IndexOf("second.mp4"));
        Assert.Contains("-maxrate",command);Assert.Contains("5000k",command);Assert.Contains("-bufsize",command);
        var silent=RecordingAssemblyCommand.Build(new(),segments,false,"final.partial.mp4");Assert.Contains("-an",silent);
        Assert.DoesNotContain("concat=n=2:v=1:a=1",silent[silent.IndexOf("-filter_complex")+1]);
    }
    [Fact]
    public async Task OneCompatibleSegmentCopiesAtomicallyAndWorkspaceCleanupOwnsAllSegments()
    {
        var workspace=RecordingWorkspace.Create(root,new(),new(0,0,100,100));var one=workspace.CreateSegment();var two=workspace.CreateSegment();
        await File.WriteAllTextAsync(one.Capture,"video");await File.WriteAllTextAsync(two.Working,"partial");
        var assembler=new RecordingAssembler(new Factory(),_=>"fixture");
        var result=await assembler.AssembleAsync(new(),workspace,[new(one.Capture,TimeSpan.FromSeconds(1),5,false,100,100)],false,default);
        Assert.True(result.Success);Assert.Equal("video",await File.ReadAllTextAsync(workspace.CapturePath));
        await workspace.DiscardAsync();Assert.False(Directory.Exists(workspace.DirectoryPath));
    }
    [Fact]
    public async Task CanceledAssemblyClosesProcessAndRetainsSourceSegments()
    {
        var workspace=RecordingWorkspace.Create(root,new(),new(0,0,100,100));var path=workspace.CreateSegment();await File.WriteAllTextAsync(path.Capture,"video");
        var factory=new Factory();using var token=new CancellationTokenSource();
        var pending=new RecordingAssembler(factory,_=>"fixture").AssembleAsync(new(),workspace,[new(path.Capture,TimeSpan.FromSeconds(1),5,true,100,100)],false,token.Token);
        await factory.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);Assert.True(factory.Process.Killed);Assert.True(factory.Process.Disposed);Assert.True(File.Exists(path.Capture));
    }
    [Fact]
    public async Task TerminationFailureStillDisposesTheAssemblyProcess()
    {
        var workspace=RecordingWorkspace.Create(root,new(),new(0,0,100,100));var path=workspace.CreateSegment();await File.WriteAllTextAsync(path.Capture,"video");
        var factory=new Factory();factory.Process.KillFails=true;using var token=new CancellationTokenSource();
        var pending=new RecordingAssembler(factory,_=>"fixture").AssembleAsync(new(),workspace,[new(path.Capture,TimeSpan.FromSeconds(1),5,true,100,100)],false,token.Token);
        await factory.Started.Task;token.Cancel();await Assert.ThrowsAnyAsync<Exception>(()=>pending);
        Assert.True(factory.Process.Disposed);Assert.True(File.Exists(path.Capture));
    }
    private sealed class Factory:IRecordingProcessFactory
    {
        public Process Process=new();public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IRecordingProcess Start(string exe,IReadOnlyList<string> args){Started.TrySetResult();return Process;}
    }
    private sealed class Process:IRecordingProcess
    {
        private readonly TaskCompletionSource<int> exit=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Ready=>Task.CompletedTask;public Task<int> Exit=>exit.Task;public string Diagnostics=>"";public bool Killed,Disposed,KillFails;
        public Task RequestStopAsync()=>Task.CompletedTask;public void Kill(){Killed=true;if(KillFails)throw new System.ComponentModel.Win32Exception("termination failed");exit.TrySetResult(-1);}public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
