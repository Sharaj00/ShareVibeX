using ShareVibeX.App.Export;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Playback;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Editing;

namespace ShareVibeX.App.Tests;

public sealed class VideoExportTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"ShareVibeX-export",Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task RepeatedCopyReusesImmutableFileAndEditsNeverModifyOriginal()
    {
        var (session,workspace,renderer)=await Create();string? first=null;
        await session.CopyAsync((path,_)=>{first=path;return Task.CompletedTask;});var originalCopy=await File.ReadAllTextAsync(first!);
        await session.CopyAsync((path,_)=>{Assert.Equal(first,path);return Task.CompletedTask;});Assert.Equal(1,renderer.Calls);
        await session.ApplyAsync(30,60,false);Assert.Equal(270,session.Timeline.FrameCount);
        Assert.Equal("original",await File.ReadAllTextAsync(workspace.CapturePath));Assert.Equal(originalCopy,await File.ReadAllTextAsync(first!));
        await session.ResetAsync();Assert.True(session.Timeline.IsOriginal);await session.DisposeAsync();Assert.True(File.Exists(first));
    }
    [Fact]
    public async Task FailedOrCanceledSavePreservesExistingDestinationAndPreview()
    {
        var (session,workspace,renderer)=await Create();var target=Path.Combine(root,"saved.mp4");await File.WriteAllTextAsync(target,"old");renderer.Fail=true;
        await Assert.ThrowsAsync<IOException>(()=>session.SaveAsync(target));Assert.Equal("old",await File.ReadAllTextAsync(target));Assert.True(session.HasUnexportedChanges);
        Assert.Equal(workspace.CapturePath,session.CurrentMedia.Path);renderer.Fail=false;await session.SaveAsync(target);
        Assert.NotEqual("old",await File.ReadAllTextAsync(target));Assert.False(session.HasUnexportedChanges);
        await Assert.ThrowsAsync<ArgumentException>(()=>session.SaveAsync(workspace.CapturePath));await session.DisposeAsync();
    }
    [Fact]
    public async Task CancelRenderingDoesNotCommitTimelineOrDeleteSource()
    {
        var (session,workspace,renderer)=await Create();renderer.Hold=true;var editing=session.ApplyAsync(30,60,true);await renderer.Started.Task;
        await session.CancelAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>editing);
        Assert.True(session.Timeline.IsOriginal);Assert.True(File.Exists(workspace.CapturePath));Assert.False(session.IsBusy);await session.DisposeAsync();
    }
    [Fact]
    public async Task FailedClipboardPublicationCanRetryTheSameFileWithoutMarkingItExported()
    {
        var (session,_,renderer)=await Create();string? first=null;
        await Assert.ThrowsAsync<IOException>(()=>session.CopyAsync((path,_)=>{first=path;throw new IOException("busy clipboard");}));Assert.True(session.HasUnexportedChanges);
        await session.CopyAsync((path,_)=>{Assert.Equal(first,path);return Task.CompletedTask;});Assert.Equal(1,renderer.Calls);Assert.False(session.HasUnexportedChanges);await session.DisposeAsync();
    }
    [Fact]
    public async Task RestoredEditsUseValidatedSourceIntervalsAndRejectTraversal()
    {
        var (session,workspace,renderer)=await Create();await session.ApplyAsync(30,90,true);var expected=session.Timeline.Fingerprint;await session.DisposeAsync();
        var source=new PreviewMedia(workspace.CapturePath,null,TimeSpan.FromSeconds(10),100,100,30,false);
        await using(var restored=new VideoExportSession(source,new(),workspace,Path.Combine(root,"clipboard"),renderer))
        {await restored.RestoreAsync();Assert.Equal(expected,restored.Timeline.Fingerprint);Assert.Equal(TimeSpan.FromSeconds(2),restored.CurrentMedia.Duration);}
        var json=await File.ReadAllTextAsync(workspace.EditManifestPath);var snapshot=System.Text.Json.JsonSerializer.Deserialize<VideoEditSnapshot>(json)!;
        await File.WriteAllTextAsync(workspace.EditManifestPath,System.Text.Json.JsonSerializer.Serialize(snapshot with{CurrentFile="../outside.mp4"}));
        await using var fallback=new VideoExportSession(source,new(),workspace,Path.Combine(root,"clipboard"),renderer);await fallback.RestoreAsync();
        Assert.True(fallback.Timeline.IsOriginal);Assert.NotNull(fallback.Warning);Assert.Equal("original",await File.ReadAllTextAsync(workspace.CapturePath));
    }
    private async Task<(VideoExportSession,RecordingWorkspace,Renderer)> Create()
    {
        var workspace=RecordingWorkspace.Create(Path.Combine(root,"recordings"),new(),new(0,0,100,100));await File.WriteAllTextAsync(workspace.CapturePath,"original");
        var renderer=new Renderer();return(new(new(workspace.CapturePath,null,TimeSpan.FromSeconds(10),100,100,30,false),new(),workspace,Path.Combine(root,"clipboard"),renderer),workspace,renderer);
    }
    private sealed class Renderer:IVideoRenderer
    {
        public int Calls;public bool Fail,Hold;public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task RenderAsync(PreviewMedia source,VideoEditTimeline edit,RecordingOptions options,string output,Action<RecordingProgress>? progress,CancellationToken token)
        {Calls++;Started.TrySetResult();if(Hold)await Task.Delay(Timeout.Infinite,token);if(Fail)throw new IOException("render failed");await File.WriteAllTextAsync(output,"rendered-"+edit.Fingerprint,token);}
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
