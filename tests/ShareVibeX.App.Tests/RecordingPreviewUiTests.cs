using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using ShareVibeX.App.Playback;
using ShareVibeX.Core.Playback;
using ShareVibeX.Core.Capture;
using R=ShareVibeX.Core.Capture.PixelRect;
using ShareVibeX.App.Export;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;

[Collection("Avalonia UI")]
public sealed class RecordingPreviewUiTests(UiTestFixture ui)
{
    [Fact]
    public Task TimeFooterIsBelowButtonsAndDoesNotMoveThemWhenHidden()=>ui.Run(async()=>
    {
        var window=new RecordingPreviewWindow(new DecoderFactory(),new AudioFactory());
        try
        {
            await window.OpenAsync(new("fixture.mp4",null,TimeSpan.FromSeconds(2),2,1,30,false),new(100,450,200,100),[new("screen",new(0,0,800,600),1)],default);
            window.ControlStrip.UpdateLayout();var anchor=window.ControlStrip.Position;
            var time=window.ControlStrip.GetLogicalDescendants().OfType<TextBlock>().Single(t=>AutomationProperties.GetName(t)=="Video time");
            var play=window.ControlStrip.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Play");
            Assert.True(time.TranslatePoint(default,window.ControlStrip)!.Value.Y>=play.TranslatePoint(default,window.ControlStrip)!.Value.Y+play.Bounds.Height);
            window.SetTimingsVisible(false);Assert.False(time.IsVisible);Assert.Equal(anchor,window.ControlStrip.Position);
        }
        finally{await window.ClosePreviewAsync();}
    });
    [Fact]
    public Task HoverTimelineLivesInsideVideoAndFrameStepsUseOutputFps()=>ui.Run(async()=>
    {
        var window=new RecordingPreviewWindow(new DecoderFactory(),new AudioFactory());
        try
        {
            await window.OpenAsync(new("fixture.mp4",null,TimeSpan.FromSeconds(2),2,1,60,false),new(100,80,200,100),[new("screen",new(0,0,800,600),1)],default);
            Assert.False(window.TimelineOverlay.IsVisible);window.SetTimelineHovered(true);Assert.True(window.TimelineOverlay.IsVisible);
            Assert.Contains(window.Range,window.GetLogicalDescendants());Assert.DoesNotContain(window.Range,window.ControlStrip.GetLogicalDescendants());
            await window.StepFrameAsync(1);Assert.InRange(window.Playback.Position.TotalSeconds,1d/60-1e-6,1d/60+1e-6);
            await window.StepFrameAsync(-1);Assert.Equal(TimeSpan.Zero,window.Playback.Position);Assert.Equal(PreviewPlaybackState.Paused,window.Playback.State);
            window.Range.Focus();window.SetTimelineHovered(false);Assert.False(window.TimelineOverlay.IsVisible);
        }
        finally{await window.ClosePreviewAsync();}
    });
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task UnsavedCloseSaveOrDiscardDrainsAndCleansOnlyTheSession(bool save)=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-close-edit",Guid.NewGuid().ToString("N"));
        var workspace=RecordingWorkspace.Create(Path.Combine(root,"sessions"),new(),new(0,0,2,1));await File.WriteAllTextAsync(workspace.CapturePath,"source");
        var exports=new VideoExportSession(new(workspace.CapturePath,null,TimeSpan.FromSeconds(2),2,1,30,false),new(),workspace,Path.Combine(root,"cache"),new Renderer());
        var target=Path.Combine(root,"saved.mp4");var window=new RecordingPreviewWindow(new DecoderFactory(),new AudioFactory());
        window.ConfigureExport(exports,workspace,(_,_)=>Task.CompletedTask,()=>Task.FromResult<string?>(target));
        try
        {
            await window.OpenAsync(exports.CurrentMedia,new(100,80,200,100),[new("screen",new(0,0,800,600),1)],default);
            var closing=window.RequestCloseAsync();for(var i=0;i<100&&!window.IsCloseConfirmationVisible;i++)await Task.Delay(10);
            Assert.True(window.IsCloseConfirmationVisible);window.ResolveClose(save?ClosePreviewChoice.Save:ClosePreviewChoice.Discard);
            Assert.True(await closing);Assert.Equal(PreviewPlaybackState.Closed,window.Playback.State);Assert.False(Directory.Exists(workspace.DirectoryPath));Assert.Equal(save,File.Exists(target));
        }
        finally{await window.ClosePreviewAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    [Fact]
    public Task NarrowMonitorKeepsPlaybackAndCloseInsideTheControlStrip()=>ui.Run(async()=>
    {
        var window=new RecordingPreviewWindow(new DecoderFactory(),new AudioFactory());
        try
        {
            await window.OpenAsync(new("fixture.mp4",null,TimeSpan.FromSeconds(2),2,1,30,false),new(100,80,100,100),[new("small",new(0,0,240,300),1)],default);
            window.ControlStrip.UpdateLayout();
            var close=window.ControlStrip.GetLogicalDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Discard video");
            var point=close.TranslatePoint(default,window.ControlStrip);Assert.NotNull(point);Assert.True(point.Value.X+close.Bounds.Width<=window.ControlStrip.Width);
        }
        finally{await window.ClosePreviewAsync();}
    });
    [Fact]
    public Task CancelDuringEditedThumbnailLoadingRestoresThePreviousPausedRevision()=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-edit-cancel",Guid.NewGuid().ToString("N"));
        var workspace=RecordingWorkspace.Create(root,new(),new(0,0,2,1));await File.WriteAllTextAsync(workspace.CapturePath,"source");
        var exports=new VideoExportSession(new(workspace.CapturePath,null,TimeSpan.FromSeconds(2),2,1,30,false),new(),workspace,Path.Combine(root,"cache"),new Renderer());
        var decoder=new BlockingEditDecoder();var window=new RecordingPreviewWindow(decoder,new AudioFactory());
        window.ConfigureExport(exports,workspace,(_,_)=>Task.CompletedTask,()=>Task.FromResult<string?>(null));
        try
        {
            await window.OpenAsync(exports.CurrentMedia,new(100,80,200,100),[new("screen",new(0,0,800,600),1)],default);
            window.Range.SetRange(15,45);var editing=window.ApplyEditAsync(true);await decoder.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            window.ControlStrip.RaiseEvent(new Avalonia.Input.KeyEventArgs{RoutedEvent=Avalonia.Input.InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Z,KeyModifiers=Avalonia.Input.KeyModifiers.Control});
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>editing);
            Assert.True(exports.Timeline.IsOriginal);Assert.Equal(TimeSpan.FromSeconds(2),window.Playback.Duration);Assert.Equal(PreviewPlaybackState.Paused,window.Playback.State);
            Assert.Equal(60,window.Range.B);Assert.Equal("source",await File.ReadAllTextAsync(workspace.CapturePath));
        }
        finally{await window.ClosePreviewAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    private sealed class BlockingEditDecoder:IPreviewDecoderFactory
    {
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IPreviewDecoder> OpenAsync(PreviewMedia media,PreviewFormat format,TimeSpan position,bool audio,CancellationToken token)
        {if(Path.GetFileName(media.Path).StartsWith("edit-")){Started.TrySetResult();await Task.Delay(Timeout.Infinite,token);}return new Decoder(format.ByteLength);}
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EditingExportsCurrentRevisionAndClosesAfterSuccessfulExport(bool save)=>ui.Run(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-edit-ui",Guid.NewGuid().ToString("N"));
        var workspace=RecordingWorkspace.Create(Path.Combine(root,"sessions"),new(),new(0,0,2,1));await File.WriteAllTextAsync(workspace.CapturePath,"source");
        var edits=new VideoExportSession(new(workspace.CapturePath,null,TimeSpan.FromSeconds(2),2,1,30,false),new(),workspace,Path.Combine(root,"cache"),new Renderer());
        var window=new RecordingPreviewWindow(new DecoderFactory(),new AudioFactory());string? copied=null,target=null,archived=null;
        var notices=new List<(string Title,string? Path)>();window.ExportCompleted=(title,path)=>notices.Add((title,path));
        window.ArchiveVideo=(path,_)=>{Assert.True(window.IsVisible);archived=path;return Task.CompletedTask;};
        window.ConfigureExport(edits,workspace,(path,_)=>{copied=path;return Task.CompletedTask;},()=>Task.FromResult(target));
        try
        {
            await window.OpenAsync(edits.CurrentMedia,new(100,80,200,100),[new("screen",new(0,0,800,600),1)],default);
            Assert.Contains(window.ControlStrip.GetLogicalDescendants().OfType<Button>(),b=>AutomationProperties.GetName(b)=="Keep A–B");
            window.SetTimelineHovered(true);window.UpdateLayout();
            Assert.True(window.Range.TranslatePoint(default,window)!.Value.Y>=0,"The seek/marker track must remain inside a small video region.");
            window.Range.SetRange(15,45);await window.ApplyEditAsync(true);Assert.Equal(TimeSpan.FromSeconds(1),window.Playback.Duration);
            var close=window.RequestCloseAsync();Assert.False(close.IsCompleted);
            for(var i=0;i<100&&!window.IsCloseConfirmationVisible;i++)await Task.Delay(10);
            Assert.True(window.IsCloseConfirmationVisible);window.ResolveClose(ClosePreviewChoice.Continue);Assert.False(await close);Assert.True(window.IsVisible);
            await window.SaveVideoAsync();Assert.True(edits.HasUnexportedChanges);Assert.Empty(notices);
            if(save){target=Path.Combine(root,"saved.mp4");await window.SaveVideoAsync();Assert.Equal(target,copied);}
            else await window.CopyVideoAsync();
            Assert.NotNull(copied);Assert.Equal(copied,archived);Assert.False(edits.HasUnexportedChanges);
            Assert.Single(notices);Assert.Equal(copied,notices[0].Path);Assert.Equal(save?"Video saved":"Video copied",notices[0].Title);
            Assert.False(window.IsVisible);Assert.False(window.ControlStrip.IsVisible);
            Assert.False(Directory.Exists(workspace.DirectoryPath));Assert.True(File.Exists(copied));
        }
        finally{await window.ClosePreviewAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    });
    private sealed class Renderer:IVideoRenderer
    {public Task RenderAsync(PreviewMedia source,VideoEditTimeline edit,RecordingOptions options,string output,Action<RecordingProgress>? progress,CancellationToken token)=>File.WriteAllTextAsync(output,"rendered",token);}
    [Theory]
    [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void PreviewUsesThePhysicalRegionAndItsMonitorScale(double scale)
    {
        var placement=PreviewWindowPlacement.ForRegion(new(-600,50,500,300),[new("left",new(-1920,0,1920,1080),scale)]);
        Assert.Equal(new Avalonia.PixelPoint(-600,50),placement.Position);Assert.Equal(new Size(500/scale,300/scale),placement.Size);
    }
    [Fact]
    public Task PreviewStartsPausedWithIconActionsAndAsyncCloseReleasesDecoder()=>ui.Run(async()=>
    {
        var factory=new DecoderFactory();var window=new RecordingPreviewWindow(factory,new AudioFactory());
        await window.OpenAsync(new("fixture.mp4",null,TimeSpan.FromSeconds(2),2,1,30,false),new R(100,80,200,100),[new("screen",new(0,0,800,600),1)],default);
        Assert.True(window.IsVisible);Assert.Equal(WindowDecorations.None,window.WindowDecorations);Assert.Equal(PreviewPlaybackState.Paused,window.Playback.State);
        var buttons=window.ControlStrip.GetLogicalDescendants().OfType<Button>().ToArray();
        Assert.Equal(new[]{"Previous frame","Play","Next frame","Discard video"},buttons.Select(AutomationProperties.GetName));Assert.All(buttons,b=>Assert.IsType<PathIcon>(b.Content));
        Assert.True(window.Range.IsEnabled);
        await window.Playback.SeekAsync(TimeSpan.FromSeconds(1));Assert.Equal(TimeSpan.FromSeconds(1),factory.LastPosition);
        await window.ClosePreviewAsync();Assert.False(window.IsVisible);Assert.False(window.ControlStrip.IsVisible);
        Assert.Equal(PreviewPlaybackState.Closed,window.Playback.State);Assert.All(factory.Opened,d=>Assert.True(d.Disposed));
    });
    private sealed class DecoderFactory:IPreviewDecoderFactory
    {
        public List<Decoder> Opened=[];public TimeSpan LastPosition;
        public Task<IPreviewDecoder> OpenAsync(PreviewMedia media,PreviewFormat format,TimeSpan position,bool audio,CancellationToken token)
        {
            LastPosition=position;var decoder=new Decoder(format.ByteLength);Opened.Add(decoder);return Task.FromResult<IPreviewDecoder>(decoder);
        }
    }
    private sealed class Decoder(int length):IPreviewDecoder
    {
        public Stream Video{get;}=new MemoryStream(new byte[length]);public Stream? Audio=>null;
        public Task VideoCompletion=>Task.CompletedTask;public Task AudioCompletion=>Task.CompletedTask;public bool Disposed;
        public ValueTask DisposeAsync(){Disposed=true;Video.Dispose();return ValueTask.CompletedTask;}
    }
    private sealed class AudioFactory:IPreviewAudioOutputFactory
    {public Task<IPreviewAudioOutput> OpenAsync(CancellationToken token)=>throw new InvalidOperationException("Paused silent preview must not open a device.");}
}

