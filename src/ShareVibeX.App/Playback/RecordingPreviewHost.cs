using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Playback;
using ShareVibeX.App.Export;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;
using ShareVibeX.Platform.Windows;
using R=ShareVibeX.Core.Capture.PixelRect;

namespace ShareVibeX.App.Playback;

public interface IRecordingPreviewHost
{
    event Action? Closed;
    Task ShowAsync(PreviewMedia media,R region,IReadOnlyList<MonitorInfo> monitors,CancellationToken token);
    Task ShowEditableAsync(PreviewMedia media,R region,IReadOnlyList<MonitorInfo> monitors,RecordingWorkspace workspace,RecordingOptions options,CancellationToken token)=>ShowAsync(media,region,monitors,token);
    Task CloseAsync();
    async Task<bool> RequestCloseAsync(){await CloseAsync();return true;}
    void Activate();
}
public sealed class RecordingPreviewHost(IPreviewDecoderFactory decoders,IPreviewAudioOutputFactory audio,ShareVibeX.App.History.CaptureHistory? history=null,Action<ShareVibeX.App.Notifications.CompletionNotice>? completed=null):IRecordingPreviewHost
{
    private RecordingPreviewWindow? window;
    public event Action? Closed;
    public async Task ShowAsync(PreviewMedia media,R region,IReadOnlyList<MonitorInfo> monitors,CancellationToken token)
        =>await ShowCoreAsync(media,region,monitors,null,null,token);
    public Task ShowEditableAsync(PreviewMedia media,R region,IReadOnlyList<MonitorInfo> monitors,RecordingWorkspace workspace,RecordingOptions options,CancellationToken token)
        =>ShowCoreAsync(media,region,monitors,workspace,options,token);
    private async Task ShowCoreAsync(PreviewMedia media,R region,IReadOnlyList<MonitorInfo> monitors,RecordingWorkspace? workspace,RecordingOptions? options,CancellationToken token)
    {
        if(window is not null)throw new InvalidOperationException(Loc.T("Close the current preview first."));
        var created=new RecordingPreviewWindow(decoders,audio);window=created;created.ExportCompleted=(title,path)=>completed?.Invoke(new(title,path,FfmpegPath:options?.FfmpegPath));
        VideoExportSession? exports=null;
        if(history is not null){var context=history.Current;created.SuggestedFolder=history.FolderFor(context.CapturedAt);created.SuggestedVideoName=Path.GetFileNameWithoutExtension(ShareVibeX.App.History.CaptureHistory.FileName(context,"mp4"));created.ArchiveVideo=async(path,ct)=>{await history.StoreFileAsync(context,path,ct);};}
        if(options is not null)created.SetTimingsVisible(options.ShowVideoTimings);
        if(workspace is not null&&options is not null)
        {
            exports=new VideoExportSession(media,options,workspace,Path.Combine(ShareVibeX.Core.IO.PortablePaths.BaseDirectory,"data","clipboard"),new VideoRenderer(new FfmpegRecordingProcessFactory()));
            created.ConfigureExport(exports,workspace,(path,ct)=>WindowsFileClipboard.PublishAsync(created.ControlStrip.TryGetPlatformHandle()?.Handle??0,path,ct));
        }
        created.Closed+=(_,_)=>{if(ReferenceEquals(window,created)){window=null;Closed?.Invoke();}};
        try{if(exports is not null){await exports.RestoreAsync(token);media=exports.CurrentMedia;created.Range.Reset(exports.Timeline.FrameCount);}await created.OpenAsync(media,region,monitors,token);}
        catch{await created.ClosePreviewAsync();throw;}
    }
    public Task CloseAsync()=>window?.ClosePreviewAsync()??Task.CompletedTask;
    public Task<bool> RequestCloseAsync()=>window?.RequestCloseAsync()??Task.FromResult(true);
    public void Activate(){window?.Activate();window?.ControlStrip.Activate();}
}

