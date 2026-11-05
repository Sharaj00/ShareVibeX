using ShareVibeX.App.Recording;
using ShareVibeX.App.History;
using ShareVibeX.Core.Settings;
using ShareVibeX.Core.Recording;
namespace ShareVibeX.App.Tests;
public sealed class WebmLifecycleTests
{
    [Fact] public async Task PreviouslySavedVp9Mp4LoadsAsMatroskaWithoutOverwritingSettings()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-vp9-migrate",Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);var file=Path.Combine(root,"settings.json");
            var json=System.Text.Json.JsonSerializer.Serialize(new AppSettings(Recording:new(){Encoder=VideoEncoder.LibvpxVp9}));
            await File.WriteAllTextAsync(file,json);var loaded=await new PortableSettingsStore(root).LoadAsync();
            Assert.Equal(VideoContainer.Matroska,loaded.RecordingDefaults.Container);Assert.Equal(json,await File.ReadAllTextAsync(file));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact] public async Task WebmWorkspaceAndHistoryRecognizeOwnedMedia()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-webm",Guid.NewGuid().ToString("N"));
        try
        {
            var options=new RecordingOptions{Encoder=VideoEncoder.LibvpxVp9,Container=VideoContainer.Webm,AudioEncoder=AudioEncoder.Libopus};
            var workspace=RecordingWorkspace.Create(root,options,new(0,0,100,100));
            Assert.True(RecordingWorkspace.IsOwnedName(Path.GetFileName(workspace.CapturePath)));
            Assert.True(RecordingWorkspace.IsOwnedName(Path.GetFileName(workspace.CreateSegment().Working)));
            Assert.True(RecordingWorkspace.IsOwnedName(Path.GetFileName(workspace.CreateDerivedFile("webm"))));
            var history=new CaptureHistory(()=>new AppSettings(HistoryEnabled:true,HistoryDirectory:Path.Combine(root,"history")));
            var path=await history.StoreAsync(new("app",DateTime.Now),"webm",(p,t)=>File.WriteAllTextAsync(p,"synthetic",t));
            Assert.EndsWith(".webm",path);Assert.Single(history.List());
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
