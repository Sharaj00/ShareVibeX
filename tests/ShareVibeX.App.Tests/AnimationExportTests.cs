using ShareVibeX.App.Export;
using ShareVibeX.App.Recording;
using ShareVibeX.App.History;
using ShareVibeX.App.Playback;
using ShareVibeX.Core.Playback;
using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Settings;
namespace ShareVibeX.App.Tests;
public sealed class AnimationExportTests
{
    [Theory][InlineData(VideoEncoder.Gif)][InlineData(VideoEncoder.LibwebpAnim)][InlineData(VideoEncoder.Apng)]
    public async Task AnimationEditsCopySaveHistoryAndRestoreKeepTheCorrectExtension(VideoEncoder encoder)
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-animation-export",Guid.NewGuid().ToString("N"));
        try
        {
            var options=new RecordingOptions().SwitchEncoder(encoder);var workspace=RecordingWorkspace.Create(root,options,new(0,0,100,100));
            await File.WriteAllTextAsync(workspace.CapturePath,"original");
            var media=new PreviewMedia(workspace.CapturePath,null,TimeSpan.FromSeconds(10),100,100,30,false);
            var cache=Path.Combine(root,"clipboard");var renderer=new Renderer();
            var export=new VideoExportSession(media,options,workspace,cache,renderer);
            await export.ApplyAsync(30,60,false);Assert.EndsWith("."+options.Extension,export.CurrentMedia.Path);
            Assert.True(RecordingWorkspace.IsOwnedName(Path.GetFileName(export.CurrentMedia.Path)));
            string? copied=null;await export.CopyAsync((p,_)=>{copied=p;return Task.CompletedTask;});
            var calls=renderer.Calls;await export.CopyAsync((p,_)=>{Assert.Equal(copied,p);return Task.CompletedTask;});Assert.Equal(calls,renderer.Calls);
            var destination=Path.Combine(root,"saved."+options.Extension);await export.SaveAsync(destination);Assert.True(File.Exists(destination));
            var wrong=Path.Combine(root,"keep.mp4");await File.WriteAllTextAsync(wrong,"existing");
            await Assert.ThrowsAsync<ArgumentException>(()=>export.SaveAsync(wrong));Assert.Equal("existing",await File.ReadAllTextAsync(wrong));
            var history=new CaptureHistory(()=>new AppSettings(HistoryEnabled:true,HistoryDirectory:Path.Combine(root,"history")));
            await history.StoreAsync(new("app",DateTime.Now),options.Extension,async(p,t)=>{await File.WriteAllBytesAsync(p,await File.ReadAllBytesAsync(copied!,t),t);});
            Assert.Single(history.List());await export.DisposeAsync();
            await using(var restored=new VideoExportSession(media,options,workspace,cache,renderer))
            {await restored.RestoreAsync();Assert.Null(restored.Warning);Assert.Equal(270,restored.Timeline.FrameCount);}
            await workspace.DiscardAsync();Assert.True(File.Exists(copied));Assert.True(File.Exists(destination));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory][InlineData("gif")][InlineData("webp")][InlineData("apng")]
    public void PreviewDecodesOnePassAndDoesNotRepeatEmbeddedLoops(string extension)
    {
        var args=PreviewDecoderCommand.Video(new("capture."+extension,null,TimeSpan.FromSeconds(10),100,100,30,false),new(100,100,30),TimeSpan.FromSeconds(1));
        Assert.Equal("1",args[args.IndexOf("-ignore_loop")+1]);Assert.True(args.IndexOf("-ignore_loop")<args.IndexOf("-i"));Assert.Contains("-an",args);
    }
    private sealed class Renderer:IVideoRenderer
    {
        public int Calls;
        public Task RenderAsync(PreviewMedia source,VideoEditTimeline edit,RecordingOptions options,string output,Action<RecordingProgress>? progress,CancellationToken token)
        {Calls++;Assert.Equal("."+options.Extension,Path.GetExtension(output));return File.WriteAllTextAsync(output,"synthetic animation",token);}
    }
}
