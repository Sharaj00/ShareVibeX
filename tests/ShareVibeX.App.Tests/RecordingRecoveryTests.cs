using ShareVibeX.App.Recovery;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;
public sealed class RecordingRecoveryTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"ShareVibeX-recovery",Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task FinalizedSessionCanReopenAndDiscardPreservesUnknownFilesAndCopies()
    {
        var recordings=Path.Combine(root,"recordings");var workspace=RecordingWorkspace.Create(recordings,new(),new(10,20,200,100));
        await File.WriteAllTextAsync(workspace.CapturePath,"video");var segment=workspace.CreateSegment();await File.WriteAllTextAsync(segment.Capture,"segment");
        await workspace.WriteManifestAsync("Result",new(true,workspace.CapturePath,null,null),segments:[new(segment.Capture,TimeSpan.FromSeconds(2),7,false,200,100)],hasAudio:false);
        var cache=Path.Combine(root,"clipboard");Directory.CreateDirectory(cache);var copied=Path.Combine(cache,"ShareVibeX-clipboard-"+Guid.NewGuid().ToString("N")+".mp4");await File.WriteAllTextAsync(copied,"copy");
        var store=new RecordingRecoveryStore(recordings,cache);var item=Assert.Single(store.List());Assert.NotNull(item.Media);Assert.Equal(TimeSpan.FromSeconds(2),item.Media.Duration);
        var unknown=Path.Combine(workspace.DirectoryPath,"notes.txt");await File.WriteAllTextAsync(unknown,"keep");await store.DiscardAsync(item);
        Assert.True(File.Exists(unknown));Assert.True(File.Exists(copied));Assert.False(File.Exists(workspace.CapturePath));
        await store.ClearClipboardAsync();Assert.False(File.Exists(copied));
    }
    [Fact]
    public async Task CorruptAndOutsideManifestsCannotAuthorizePreviewOrDeletion()
    {
        var workspace=RecordingWorkspace.Create(Path.Combine(root,"recordings"),new(),new(0,0,100,100));
        await File.WriteAllTextAsync(workspace.ManifestPath,"{\"Schema\":2,\"Options\":{},\"Region\":{\"Width\":100,\"Height\":100},\"Owned\":[\"../outside.mp4\"]}");
        var store=new RecordingRecoveryStore(Path.Combine(root,"recordings"),Path.Combine(root,"clipboard"));var entry=Assert.Single(store.List());
        Assert.Null(entry.Workspace);Assert.Null(entry.Media);Assert.NotNull(entry.Error);await Assert.ThrowsAsync<IOException>(()=>store.DiscardAsync(entry));
        Assert.True(File.Exists(workspace.ManifestPath));
    }
    [Fact]
    public async Task NullSegmentMetadataIsRetainedWithoutCrashingTheRecoveryList()
    {
        var recordings=Path.Combine(root,"recordings");var workspace=RecordingWorkspace.Create(recordings,new(),new(0,0,100,100));
        await workspace.WriteManifestAsync("Paused",segments:[]);
        var json=await File.ReadAllTextAsync(workspace.ManifestPath);await File.WriteAllTextAsync(workspace.ManifestPath,json.Replace("\"Segments\":[]","\"Segments\":[null]"));
        var item=Assert.Single(new RecordingRecoveryStore(recordings,Path.Combine(root,"clipboard")).List());Assert.NotNull(item.Error);Assert.Null(item.Workspace);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
